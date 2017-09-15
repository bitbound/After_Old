﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Threading;
using Translucency.WebSockets;
using System.Linq;
using After.Models;
using System.Collections.Generic;
using Dynamic_JSON;

namespace After
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvcCore().AddRazorPages((options) =>
            {
                options.Conventions.AllowAnonymousToPage("/");
            });
            services.AddAuthorization(options => { options.DefaultPolicy = null; });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            Utilities.RootPath = System.IO.Path.Combine(env.ContentRootPath, "wwwroot");
            After.Utilities.StartUp();
            WebSocketServer.Create("After");
            app.UseStaticFiles();
            var webSocketOptions = new WebSocketOptions()
            {
                ReceiveBufferSize = 10 * 1024
            };
            app.UseWebSockets(webSocketOptions);
            app.Use(async (context, next) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var client = new WebSocketClient(WebSocketServer.ServerList["After"], webSocket);
                    client.OnMessageStringPreAction = (wsClient, jsonMessage) =>
                    {
                        string category = jsonMessage.Category;
                        string type = jsonMessage.Type;
                        if (jsonMessage == null || String.IsNullOrEmpty(category) || String.IsNullOrEmpty(type))
                        {
                            throw new Exception("Category or Type is null within Socket_Handler.OnMessage.");
                        }

                        if (wsClient.Tags.Authenticated != true)
                        {
                            if (category != "Accounts" || (type != "Logon" && type != "AccountCreation" && type != "ForgotPassword"))
                            {
                                webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Unauthorized.", CancellationToken.None);
                                return;
                            }
                        }
                        var methodHandler = Type.GetType("After.Message_Handlers." + category).GetMethods().FirstOrDefault(mi => mi.Name == "Handle" + type);
                        if (methodHandler != null)
                        {
                            try
                            {
                                if (category == "Messages" && type == "Admin")
                                {
                                    After.Message_Handlers.Messages.HandleAdmin(jsonMessage, client);
                                }
                                else
                                {
                                    methodHandler.Invoke(null, new object[] { jsonMessage, client });
                                }
                            }
                            catch (Exception ex)
                            {
                                After.Utilities.WriteError(ex);
                            }
                        }
                    };
                    client.OnCloseAction = (wsClient) =>
                    {
                        var clientList = WebSocketServer.ServerList["After"].ClientList;
                        if (clientList.Contains(wsClient))
                        {
                            clientList.Remove(wsClient);
                        }
                        if (wsClient.Tags["Player"]?.Name == null)
                        {
                            return;
                        }
                        dynamic message = new
                        {
                            Category = "Accounts",
                            Type = "Disconnected",
                            Username = wsClient.Tags["Player"].Name
                        };
                        WebSocketServer.ServerList["After"].Broadcast(JSON.Encode(message), wsClient);
                        var player = wsClient.Tags["Player"] as Player;
                        Storage.Current.Players.Store(player.StorageID);
                        player.GetCurrentLocation()?.CharacterLeavesAsync(player);
                        foreach (var timer in player.Timers)
                        {
                            timer.Value.Stop();
                            timer.Value.Dispose();
                        }
                    };
                    Utilities.Server.ClientList.Add(client);
                    await client.HandleSocket();
                }
                else
                {
                    await next();
                }
            });
            app.UseMvc();
            //app.Run(async (context) =>
            //{
            //    await context.Response.WriteAsync("Hello World!");
            //});
        }
    }
}

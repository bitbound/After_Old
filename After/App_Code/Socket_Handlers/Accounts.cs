﻿using After.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Helpers;

namespace After.Socket_Handlers
{
    public static class Accounts
    {
        public static void HandleAccountCreation(dynamic jsonMessage, Socket_Handler SH)
        {
            string name = jsonMessage.Username;
            if (World.Current.Players.FirstOrDefault((p => p.Name == name)) != null)
            {
                jsonMessage.Result = "exists";
                jsonMessage.Password = null;
                SH.Send(Json.Encode(jsonMessage));
                return;
            }
            else
            {
                SH.Authenticated = true;
                SH.Player = new Player()
                {
                    Name = name,
                    Color = jsonMessage.Color,
                    Password = Crypto.HashPassword(jsonMessage.Password),
                    // TODO: Add void area interaction.
                    CurrentXYZ = "0,0,0"
                };
                World.Current.Players.Add(SH.Player);
                Socket_Handler.SocketCollection.Add(SH);
                jsonMessage.Result = "ok";
                jsonMessage.Password = null;
                Socket_Handler.SocketCollection.Broadcast(Json.Encode(jsonMessage));
            }
        }
        public static void HandleLogon(dynamic jsonMessage, Socket_Handler SH)
        {
            var playerName = (string)jsonMessage.Username;
            SH.Player = World.Current.Players.FirstOrDefault(p => p.Name == playerName);
            if (SH.Player == null)
            {
                jsonMessage.Result = "failed";
                SH.Send(Json.Encode(jsonMessage));
                return;
            }
            if (!Crypto.VerifyHashedPassword(SH.Player.Password, jsonMessage.Password))
            {
                jsonMessage.Result = "failed";
                SH.Send(Json.Encode(jsonMessage));
                return;
            }
            SH.Authenticated = true;
            Socket_Handler.SocketCollection.Add(SH);
            jsonMessage.Result = "ok";
            Socket_Handler.SocketCollection.Broadcast(Json.Encode(jsonMessage));
        }
    }
}
﻿using After.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Translucency.WebSockets;

namespace After.Message_Handlers
{
    public static class Queries
    {
        public static void HandlePlayerUpdate(dynamic JsonMessage, WebSocketClient WSC)
        {
            JsonMessage.Player = (WSC.Tags["Player"] as Player).ConvertToMe();
            WSC.SendString(JsonConvert.SerializeObject(JsonMessage));
        }
        public static void HandleRefreshView(dynamic JsonMessage, WebSocketClient WSC)
        {
            var souls = new List<dynamic>();
            var areas = new List<dynamic>();
            var location = (WSC.Tags["Player"] as Player).GetCurrentLocation();
            if (location == null)
            {
                return;
            }
            foreach (var area in (WSC.Tags["Player"] as Player).GetVisibleLocations())
            {
                if (area.IsStatic == false && DateTime.Now - area.LastVisited > TimeSpan.FromMinutes(1))
                {
                    var request = new
                    {
                        Category = "Events",
                        Type = "AreaRemoved",
                        Area = area.ConvertToArea(true)
                    };
                    foreach (var player in area.GetNearbyPlayers())
                    {
                        player.SendString(JsonConvert.SerializeObject(request));
                    }
                    Storage.Current.Locations.Remove(area.StorageID);
                    continue;
                }
                foreach (var occupant in area.Occupants)
                {
                    Character character = Storage.Current.NPCs.Find(occupant);
                    if (character == null)
                    {
                        character = Storage.Current.Players.Find(occupant);
                        if (character == null)
                        {
                            continue;
                        }
                    }
                    if (character is Player && !(character as Player).IsLoggedIn())
                    {
                        continue;
                    }
                    souls.Add(character.ConvertToSoul());
                }
                areas.Add(area.ConvertToArea(true));
            }
            JsonMessage.Souls = souls;
            JsonMessage.Areas = areas;
            WSC.SendString(JsonConvert.SerializeObject(JsonMessage));
        }

        public static void HandleGetPowers(dynamic JsonMessage, WebSocketClient WSC)
        {
            JsonMessage.Powers = (WSC.Tags["Player"] as Player).Powers;
            WSC.SendString(JsonConvert.SerializeObject(JsonMessage));
        }
        public static void HandleFirstLoad(dynamic JsonMessage, WebSocketClient WSC)
        {
            if (String.IsNullOrWhiteSpace((WSC.Tags["Player"] as Player).CurrentXYZ))
            {
                if (String.IsNullOrWhiteSpace((WSC.Tags["Player"] as Player).PreviousXYZ))
                {
                    (WSC.Tags["Player"] as Player).CurrentXYZ = "0,0,0";
                }
                else
                {
                    (WSC.Tags["Player"] as Player).CurrentXYZ = (WSC.Tags["Player"] as Player).PreviousXYZ;
                }
            }
            if (!Storage.Current.Locations.Exists((WSC.Tags["Player"] as Player).CurrentXYZ))
            {
                (WSC.Tags["Player"] as Player).CurrentXYZ = "0,0,0";
            }
            JsonMessage.Settings = (WSC.Tags["Player"] as Player).Settings;
            JsonMessage.Player = (WSC.Tags["Player"] as Player).ConvertToMe();
            JsonMessage.Powers = (WSC.Tags["Player"] as Player).Powers;
            JsonMessage.AccountType = (WSC.Tags["Player"] as Player).AccountType;
            WSC.SendString(JsonConvert.SerializeObject(JsonMessage));

            (WSC.Tags["Player"] as Player).GetCurrentLocation().CharacterArrives((WSC.Tags["Player"] as Player));
        }
        public static async Task HandleMapUpdate(dynamic JsonMessage, WebSocketClient WSC)
        {
            await Task.Run(()=>{
                var visibleLocations = (WSC.Tags["Player"] as Player).GetVisibleLocations();
                for (var x = JsonMessage.XMin; x <= JsonMessage.XMax; x++)
                {
                    for (var y = JsonMessage.YMin; y <= JsonMessage.YMax; y++)
                    {
                        var location = Storage.Current.Locations.Find($"{x},{y},{(WSC.Tags["Player"] as Player).ZCoord}");
                        var landmark = Storage.Current.Landmarks.Find($"{x},{y},{(WSC.Tags["Player"] as Player).ZCoord}");
                        if (location != null)
                        {
                            if (!visibleLocations.Contains(location))
                            {
                                var request = new
                                {
                                    Category = "Queries",
                                    Type = "MapUpdate",
                                    Area = location.ConvertToArea(false)
                                };
                                WSC.SendString(JsonConvert.SerializeObject(request));
                            }
                        }
                        if (landmark != null)
                        {
                            var request = new
                            {
                                Category = "Queries",
                                Type = "MapUpdate",
                                Landmark = landmark.ConvertToDynamic()
                            };
                            WSC.SendString(JsonConvert.SerializeObject(request));
                        }
                    }
                }
                var done = new
                {
                    Category = "Queries",
                    Type = "MapUpdate",
                    Completed = true
                };
                WSC.SendString(JsonConvert.SerializeObject(done));
            });
        }
        public static void HandleGetAreaActions(dynamic JsonMessage, WebSocketClient WSC)
        {
            var actionList = new List<string>();
            Location target = Storage.Current.Locations.Find(JsonMessage.TargetXYZ);
            var distance = target.GetDistanceFrom(Storage.Current.Locations.Find((WSC.Tags["Player"] as Player).CurrentXYZ));
            if (distance == 0)
            {
                actionList.Add("Explore Here");
                if (!target.IsStatic || target.OwnerID != (WSC.Tags["Player"] as Player).StorageID)
                {
                    actionList.Add("Take Control");
                }
            }
            else if (distance < 2)
            {
                actionList.Add("Move Here");
                if (target.OwnerID == (WSC.Tags["Player"] as Player).StorageID)
                {
                    actionList.Add("Change Area");
                }
                actionList.Add("Destroy");
            }
            foreach (var power in (WSC.Tags["Player"] as Player).Powers.FindAll(p=> p.TargetList.Contains(Power.Targets.Location) && distance >= p.MinRange && distance <= p.MaxRange))
            {
                actionList.Add(power.Name);
            }
            if (actionList.Count > 0)
            {
                JsonMessage.Actions = actionList;
                WSC.SendString(JsonConvert.SerializeObject(JsonMessage));
            }
        }
    }
}
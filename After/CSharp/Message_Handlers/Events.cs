﻿using After.Models;
using Translucency.WebSockets;

namespace After.Message_Handlers
{
    public static class Events
    {
        public static void HandleStartCharging(dynamic JsonMessage, WebSocketClient WSC)
        {
            if ((WSC.Tags["Player"] as Player).MovementState != Models.Character.MovementStates.Ready)
            {
                return;
            }
            (WSC.Tags["Player"] as Player).StartCharging();
        }
        public static void HandleStopCharging(dynamic JsonMessage, WebSocketClient WSC)
        {
            if ((WSC.Tags["Player"] as Player).MovementState != Models.Character.MovementStates.Ready)
            {
                return;
            }
            (WSC.Tags["Player"] as Player).StopCharging();
        }
        public static void HandlePlayerMove(dynamic JsonMessage, WebSocketClient WSC)
        {
            if ((WSC.Tags["Player"] as Player).MovementState != Models.Character.MovementStates.Ready)
            {
                return;
            }
            var xChange = 0;
            var yChange = 0;
            var dir = (string)JsonMessage.Direction;
            dir = dir.ToUpper();
            if (dir.Contains("N"))
            {
                yChange--;
            }
            else if (dir.Contains("S"))
            {
                yChange++;
            }
            if (dir.Contains("E"))
            {
                xChange++;
            }
            else if (dir.Contains("W"))
            {
                xChange--;
            }
            var currentXYZ = (WSC.Tags["Player"] as Player).CurrentXYZ.Split(',');
            var destXYZ = new string[3];
            destXYZ[0] = (double.Parse(currentXYZ[0]) + xChange).ToString();
            destXYZ[1] = (double.Parse(currentXYZ[1]) + yChange).ToString();
            destXYZ[2] = currentXYZ[2];
            (WSC.Tags["Player"] as Player).Move(destXYZ);
        }
    }
}
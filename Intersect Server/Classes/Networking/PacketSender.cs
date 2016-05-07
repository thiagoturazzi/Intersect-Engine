﻿/*
    Intersect Game Engine (Server)
    Copyright (C) 2015  JC Snider, Joe Bridges
    
    Website: http://ascensiongamedev.com
    Contact Email: admin@ascensiongamedev.com 

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along
    with this program; if not, write to the Free Software Foundation, Inc.,
    51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Intersect_Library;
using Intersect_Library.GameObjects;
using Intersect_Library.GameObjects.Events;
using Intersect_Library.GameObjects.Maps;
using Intersect_Library.GameObjects.Maps.MapList;
using Intersect_Server.Classes.Core;
using Intersect_Server.Classes.Entities;
using Intersect_Server.Classes.General;
using Intersect_Server.Classes.Maps;


namespace Intersect_Server.Classes.Networking
{
    public static class PacketSender
    {

        public static void SendDataToMap(int mapNum, byte[] data)
        {
            if (!MapInstance.GetObjects().ContainsKey(mapNum)) { return; }
            List<int> Players = MapInstance.GetMap(mapNum).GetPlayersOnMap();
            for (int i = 0; i < Players.Count; i++)
            {
                Globals.Clients[Players[i]].SendPacket(data);
            }
        }
        public static void SendDataToProximity(int mapNum, byte[] data)
        {
            if (!MapInstance.GetObjects().ContainsKey(mapNum)) { return; }
            SendDataToMap(mapNum, data);
            for (int i = 0; i < MapInstance.GetMap(mapNum).SurroundingMaps.Count; i++)
            {
                SendDataToMap(MapInstance.GetMap(mapNum).SurroundingMaps[i], data);
            }
        }
        public static void SendDataToEditors(byte[] data)
        {
            for (var i = 0; i < Globals.Clients.Count; i++)
            {
                if (Globals.Clients[i] == null) continue;
                if (!Globals.Clients[i].IsConnected()) continue;
                if (Globals.Clients[i].IsEditor)
                {
                    Globals.Clients[i].SendPacket(data);
                }
            }
        }

        public static void SendPing(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.RequestPing);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
            client.ConnectionTimeout = Environment.TickCount + (client.TimeoutLength * 1000);
        }

        public static void SendServerConfig(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.ServerConfig);
            bf.WriteBytes(ServerOptions.GetServerConfig());
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendJoinGame(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.JoinGame);
            bf.WriteLong(client.EntityIndex);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendMap(Client client, int mapNum)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.MapData);
            bf.WriteLong(mapNum);
            bool isEditor = false;
            if (client != null && client.IsEditor) isEditor = true;
            byte[] MapData = null;
            if (isEditor)
            {
                MapData = MapInstance.GetMap(mapNum).GetEditorMapData();
                bf.WriteLong(MapData.Length);
                bf.WriteBytes(MapData);
            }
            else
            {
                MapData = MapInstance.GetMap(mapNum).GetClientMapData();
                bf.WriteLong(MapData.Length);
                bf.WriteBytes(MapData);
                bf.WriteInteger(MapInstance.GetMap(mapNum).Revision);
                bf.WriteInteger(MapInstance.GetMap(mapNum).MapGridX);
                bf.WriteInteger(MapInstance.GetMap(mapNum).MapGridY);
                if (Options.GameBorderStyle == 1)
                {
                    bf.WriteInteger(1);
                    bf.WriteInteger(1);
                    bf.WriteInteger(1);
                    bf.WriteInteger(1);
                }
                else if (Options.GameBorderStyle == 0)
                {
                    if (0 == MapInstance.GetMap(mapNum).MapGridX)
                    {
                        bf.WriteInteger(1);
                    }
                    else
                    {
                        bf.WriteInteger(0);
                    }
                    if (Database.MapGrids[MapInstance.GetMap(mapNum).MapGrid].XMax - 1 == MapInstance.GetMap(mapNum).MapGridX)
                    {
                        bf.WriteInteger(1);
                    }
                    else
                    {
                        bf.WriteInteger(0);
                    }
                    if (0 == MapInstance.GetMap(mapNum).MapGridY)
                    {
                        bf.WriteInteger(1);
                    }
                    else
                    {
                        bf.WriteInteger(0);
                    }
                    if (Database.MapGrids[MapInstance.GetMap(mapNum).MapGrid].YMax - 1 == MapInstance.GetMap(mapNum).MapGridY)
                    {
                        bf.WriteInteger(1);
                    }
                    else
                    {
                        bf.WriteInteger(0);
                    }
                }
                else
                {
                    bf.WriteInteger(0);
                    bf.WriteInteger(0);
                    bf.WriteInteger(0);
                    bf.WriteInteger(0);
                }
            }
            if (client != null)
            {
                client.SendPacket(bf.ToArray());
                if (isEditor)
                {
                    SendDataToEditors(bf.ToArray());
                }
                else
                {
                    MapInstance.GetMap(mapNum).SendMapEntitiesTo(client);
                }
            }
            else if (client == null)
            {
                SendDataToProximity(mapNum, bf.ToArray());
                SendMapItemsToProximity(mapNum);
                SendDataToEditors(bf.ToArray());
            }
            bf.Dispose();
        }

        public static void SendMapToEditors(int mapNum)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.MapData);
            bf.WriteLong(mapNum);
            byte[] MapData = MapInstance.GetMap(mapNum).GetEditorMapData();
            bf.WriteLong(MapData.Length);
            bf.WriteBytes(MapData);
            SendDataToEditors(bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityDataTo(Client client, int sendIndex, int type, byte[] data, Entity en)
        {
            if (sendIndex == -1) { return; }
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityData);
            bf.WriteLong(sendIndex);
            bf.WriteInteger(type);
            bf.WriteBytes(data);

            client.SendPacket(bf.ToArray());
            bf.Dispose();
            SendEntityVitalsTo(client, sendIndex, type, en);
            SendEntityStatsTo(client, sendIndex, type, en);
            SendEntityPositionTo(client, sendIndex, type, en);

            if (en == client.Entity)
            {
                SendExperience(client);
                SendInventory(client);
                SendPlayerSpells(client);
                SendPlayerEquipmentToProximity(client.Entity);
                SendPointsTo(client);
                SendHotbarSlots(client);
            }
        }

        public static void SendEntityDataToProximity(int entityIndex, int type, byte[] data, Entity en)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityData);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteBytes(data);
            SendDataToProximity(en.CurrentMap, bf.ToArray());
            bf.Dispose();
            SendEntityVitals(entityIndex, type, en);
            SendEntityStats(entityIndex, type, en);
        }

        public static void SendEntityPositionTo(Client client, int entityIndex, int type, Entity en)
        {
            if (en == null) { return; }
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityPosition);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(en.CurrentMap);
            bf.WriteInteger(en.CurrentX);
            bf.WriteInteger(en.CurrentY);
            bf.WriteInteger(en.Dir);
            bf.WriteInteger(en.Passable);
            bf.WriteInteger(en.HideName);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityPositionToAll(int entityIndex, int type, Entity en)
        {
            if (en == null) { return; }
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityPosition);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(en.CurrentMap);
            bf.WriteInteger(en.CurrentX);
            bf.WriteInteger(en.CurrentY);
            bf.WriteInteger(en.Dir);
            bf.WriteInteger(en.Passable);
            bf.WriteInteger(en.HideName);
            SendDataToProximity(en.CurrentMap, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityLeave(int entityIndex, int type, int mapNum)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityLeave);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(mapNum);
            SendDataToProximity(mapNum, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityLeaveTo(Client client, int entityIndex, int type, int map)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityLeave);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(map);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendDataToAll(byte[] packet)
        {
            foreach (var t in Globals.Clients)
            {
                if (t == null) continue;
                if (t.IsConnected() && (t.IsEditor || ((Player)Globals.Entities[t.EntityIndex]).InGame))
                {
                    t.SendPacket(packet);
                }
            }
        }

        public static void SendDataTo(Client client, byte[] packet)
        {
            client.SendPacket(packet);
        }

        public static void SendPlayerMsg(Client client, string message)
        {
            SendPlayerMsg(client, message, Color.Black);
        }

        public static void SendPlayerMsg(Client client, string message, Color clr)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.ChatMessage);
            bf.WriteString(message);
            bf.WriteByte(clr.A);
            bf.WriteByte(clr.R);
            bf.WriteByte(clr.G);
            bf.WriteByte(clr.B);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendGameData(Client client)
        {
            //Send massive amounts of game data
            foreach (var val in Enum.GetValues(typeof(GameObject)))
            {
                if ((GameObject) val == GameObject.Map) continue;
                if (((GameObject)val == GameObject.Shop ||
                    (GameObject)val == GameObject.CommonEvent ||
                    (GameObject)val == GameObject.PlayerSwitch ||
                    (GameObject)val == GameObject.PlayerVariable ||
                    (GameObject)val == GameObject.ServerSwitch ||
                    (GameObject)val == GameObject.ServerVariable) && !client.IsEditor) continue;
                SendGameObjects(client,(GameObject)val);
            }
            //Let the client/editor know they have everything now
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.GameData);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendGlobalMsg(string message)
        {
            SendGlobalMsg(message, Color.Black);
        }

        public static void SendGlobalMsg(string message, Color clr)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.ChatMessage);
            bf.WriteString(message);
            bf.WriteByte(clr.A);
            bf.WriteByte(clr.R);
            bf.WriteByte(clr.G);
            bf.WriteByte(clr.B);
            SendDataToAll(bf.ToArray());
            bf.Dispose();
        }

        public static void SendProximityMsg(string message, int centerMap)
        {
            SendProximityMsg(message, centerMap, Color.Black);
        }

        public static void SendProximityMsg(string message, int centerMap, Color clr)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.ChatMessage);
            bf.WriteString(message);
            bf.WriteByte(clr.A);
            bf.WriteByte(clr.R);
            bf.WriteByte(clr.G);
            bf.WriteByte(clr.B);
            SendDataToProximity(centerMap, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEnterMap(Client client, int mapNum)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EnterMap);
            bf.WriteLong(mapNum);
            if (!(MapInstance.GetMap(mapNum).MapGridX == -1 || MapInstance.GetMap(mapNum).MapGridY == -1))
            {
                if (!client.IsEditor){  MapInstance.GetMap(mapNum).PlayerEnteredMap(client);}
                for (var y = MapInstance.GetMap(mapNum).MapGridY - 1; y < MapInstance.GetMap(mapNum).MapGridY + 2; y++)
                {
                    for (var x = MapInstance.GetMap(mapNum).MapGridX - 1; x < MapInstance.GetMap(mapNum).MapGridX + 2; x++)
                    {
                        if (x >= Database.MapGrids[MapInstance.GetMap(mapNum).MapGrid].XMin && x < Database.MapGrids[MapInstance.GetMap(mapNum).MapGrid].XMax && y >= Database.MapGrids[MapInstance.GetMap(mapNum).MapGrid].YMin && y < Database.MapGrids[MapInstance.GetMap(mapNum).MapGrid].YMax)
                        {
                            bf.WriteLong(Database.MapGrids[MapInstance.GetMap(mapNum).MapGrid].MyGrid[x, y]);
                        }
                        else
                        {
                            bf.WriteLong(-1);
                        }
                        
                    }
                }
                client.SendPacket(bf.ToArray());
                
                //Send Map Info
                for (int i = 0; i < MapInstance.GetMap(mapNum).SurroundingMaps.Count; i++)
                {
                    PacketSender.SendMapItems(client, MapInstance.GetMap(mapNum).SurroundingMaps[i]);
                }
            }
            bf.Dispose();
        }

        public static void SendDataToAllBut(int index, byte[] packet, bool entityId)
        {
            for (var i = 0; i < Globals.Clients.Count; i++)
            {
                if (Globals.Clients[i] == null) continue;
                if ((!entityId || Globals.Clients[i].EntityIndex == index) && (entityId || i == index)) continue;
                if (!Globals.Clients[i].IsConnected() || Globals.Clients[i].EntityIndex <= -1) continue;
                if (Globals.Entities[Globals.Clients[i].EntityIndex] == null) continue;
                if (((Player)Globals.Entities[Globals.Clients[i].EntityIndex]).InGame)
                {
                    Globals.Clients[i].SendPacket(packet);
                }
            }
        }

        internal static void SendRemoveProjectileSpawn(int map, int baseEntityIndex, int spawnIndex)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.ProjectileSpawnDead);
            bf.WriteLong(baseEntityIndex);
            bf.WriteLong(spawnIndex);
            SendDataToProximity(map, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityMove(int entityIndex, int type, Entity en, int correction = 0)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityMove);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(en.CurrentMap);
            bf.WriteInteger(en.CurrentX);
            bf.WriteInteger(en.CurrentY);
            bf.WriteInteger(en.Dir);
            bf.WriteInteger(correction);
            SendDataToProximity(en.CurrentMap, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityMoveTo(Client client, int entityIndex, int type, Entity en, int correction = 0)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityMove);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(en.CurrentMap);
            bf.WriteInteger(en.CurrentX);
            bf.WriteInteger(en.CurrentY);
            bf.WriteInteger(en.Dir);
            bf.WriteInteger(correction);
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityVitals(int entityIndex, int type, Entity en)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityVitals);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(en.CurrentMap);
            for (var i = 0; i < (int)Vitals.VitalCount; i++)
            {
                bf.WriteInteger(en.MaxVital[i]);
                bf.WriteInteger(en.Vital[i]);
            }
            bf.WriteInteger(en.Status.Count);
            for (var i = 0; i < en.Status.Count; i++)
            {
                bf.WriteInteger(en.Status[i].Type);
                bf.WriteString(en.Status[i].Data);
            }
            SendDataToProximity(en.CurrentMap, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityStats(int entityIndex, int type, Entity en)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityStats);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(en.CurrentMap);
            for (var i = 0; i < (int)Stats.StatCount; i++)
            {
                bf.WriteInteger(en.Stat[i].Value());
            }
            SendDataToProximity(en.CurrentMap, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityVitalsTo(Client client, int entityIndex, int type, Entity en)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityVitals);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(en.CurrentMap);
            for (var i = 0; i < (int)Vitals.VitalCount; i++)
            {
                bf.WriteInteger(en.MaxVital[i]);
                bf.WriteInteger(en.Vital[i]);
            }
            bf.WriteInteger(en.Status.Count);
            for (var i = 0; i < en.Status.Count; i++)
            {
                bf.WriteInteger(en.Status[i].Type);
                bf.WriteString(en.Status[i].Data);
            }
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityStatsTo(Client client, int entityIndex, int type, Entity en)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityStats);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(en.CurrentMap);
            for (var i = 0; i < (int)Stats.StatCount; i++)
            {
                bf.WriteInteger(en.Stat[i].Value());
            }
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityDir(int entityIndex, int type, int dir, int map)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityDir);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(map);
            bf.WriteInteger(dir);
            SendDataToProximity(map, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityDirTo(Client client, int entityIndex, int type, int dir, int map)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EntityDir);
            bf.WriteLong(entityIndex);
            bf.WriteInteger(type);
            bf.WriteInteger(map);
            bf.WriteInteger(dir);
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }

        public static void SendEventDialog(Client client, string prompt,string face, int mapNum, int eventIndex)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EventDialog);
            bf.WriteString(prompt);
            bf.WriteString(face);
            bf.WriteInteger(0);
            bf.WriteInteger(mapNum);
            bf.WriteInteger(eventIndex);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }
        public static void SendEventDialog(Client client, string prompt, string opt1, string opt2, string opt3, string opt4,string face,int mapNum, int eventIndex)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.EventDialog);
            bf.WriteString(prompt);
            bf.WriteString(face);
            bf.WriteInteger(1);
            bf.WriteString(opt1);
            bf.WriteString(opt2);
            bf.WriteString(opt3);
            bf.WriteString(opt4);
            bf.WriteInteger(mapNum);
            bf.WriteInteger(eventIndex);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendMapList(Client client)
        {
            var bf = new ByteBuffer();
            Dictionary<int, MapBase> gameMaps = MapInstance.GetObjects().ToDictionary(k => k.Key, v => (MapBase)v.Value);
            bf.WriteLong((int)ServerPackets.MapList);
            bf.WriteBytes(MapList.GetList().Data(gameMaps));
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendMapListToAll()
        {
            var bf = new ByteBuffer();
            Dictionary<int, MapBase> gameMaps = MapInstance.GetObjects().ToDictionary(k => k.Key, v => (MapBase)v.Value);
            bf.WriteLong((int)ServerPackets.MapList);
            bf.WriteBytes(MapList.GetList().Data(gameMaps));
            SendDataToAll(bf.ToArray());
            bf.Dispose();
        }

        public static void SendLoginError(Client client, string error)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.LoginError);
            bf.WriteString(error);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendMapItems(Client client, int mapNum)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.MapItems);
            bf.WriteInteger(mapNum);
            bf.WriteInteger(MapInstance.GetMap(mapNum).MapItems.Count);
            for (int i = 0; i < MapInstance.GetMap(mapNum).MapItems.Count; i++)
            {
                bf.WriteBytes(MapInstance.GetMap(mapNum).MapItems[i].Data());
            }
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendMapItemsToProximity(int mapNum)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.MapItems);
            bf.WriteInteger(mapNum);
            bf.WriteInteger(MapInstance.GetMap(mapNum).MapItems.Count);
            for (int i = 0; i < MapInstance.GetMap(mapNum).MapItems.Count; i++)
            {
                bf.WriteBytes(MapInstance.GetMap(mapNum).MapItems[i].Data());
            }
            SendDataToProximity(mapNum, bf.ToArray());
            bf.Dispose();
        }

        public static void SendMapItemUpdate(int mapNum, int index)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.MapItemUpdate);
            bf.WriteInteger(mapNum);
            bf.WriteInteger(index);
            if (MapInstance.GetMap(mapNum).MapItems[index].ItemNum == -1)
            {
                bf.WriteInteger(-1);
            }
            else
            {
                bf.WriteInteger(1);
                bf.WriteBytes(MapInstance.GetMap(mapNum).MapItems[index].Data());
            }
            SendDataToProximity(mapNum, bf.ToArray());
            bf.Dispose();
        }

        public static void SendInventory(Client client)
        {
            for (int i = 0; i < Options.MaxInvItems; i++)
            {
                SendInventoryItemUpdate(client, i);
            }
        }
        public static void SendInventoryItemUpdate(Client client, int slot)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.InventoryUpdate);
            bf.WriteInteger(slot);
            bf.WriteBytes(client.Entity.Inventory[slot].Data());
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }
        public static void SendPlayerSpells(Client client)
        {
            for (int i = 0; i < Options.MaxPlayerSkills; i++)
            {
                SendPlayerSpellUpdate(client, i);
            }
        }
        public static void SendPlayerSpellUpdate(Client client, int slot)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.SpellUpdate);
            bf.WriteInteger(slot);
            bf.WriteBytes(client.Entity.Spells[slot].Data());
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }
        public static void SendPlayerEquipmentTo(Client client, Player en)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.PlayerEquipment);
            bf.WriteInteger(en.MyClient.EntityIndex);
            for (int i = 0; i < Options.EquipmentSlots.Count; i++)
            {
                bf.WriteInteger(en.Equipment[i]);
            }
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }
        public static void SendPlayerEquipmentToProximity(Player en)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.PlayerEquipment);
            bf.WriteInteger(en.MyClient.EntityIndex);
            for (int i = 0; i < Options.EquipmentSlots.Count; i++)
            {
                bf.WriteInteger(en.Equipment[i]);
            }
            SendDataToProximity(en.CurrentMap, bf.ToArray());
            bf.Dispose();
        }
        public static void SendPointsTo(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.StatPoints);
            bf.WriteInteger(client.Entity.StatPoints);
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }

        public static void SendHotbarSlots(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.HotbarSlots);
            for (int i = 0; i < Options.MaxHotbar; i++)
            {
                bf.WriteInteger(client.Entity.Hotbar[i].Type);
                bf.WriteInteger(client.Entity.Hotbar[i].Slot);
            }
            SendDataTo(client, bf.ToArray());
            bf.Dispose();
        }

        public static void SendCreateCharacter(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.CreateCharacter);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendOpenAdminWindow(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.OpenAdminWindow);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendMapGrid(Client client, int gridIndex)
        {
            ByteBuffer bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.MapGrid);
            bf.WriteLong(Database.MapGrids[gridIndex].Width);
            bf.WriteLong(Database.MapGrids[gridIndex].Height);
            for (int x = 0; x < Database.MapGrids[gridIndex].Width; x++)
            {
                for (int y = 0; y < Database.MapGrids[gridIndex].Height; y++)
                {
                    bf.WriteInteger(Database.MapGrids[gridIndex].MyGrid[x,y]);
                    if (Database.MapGrids[gridIndex].MyGrid[x, y] != -1)
                    {
                        bf.WriteString(MapInstance.GetMap(Database.MapGrids[gridIndex].MyGrid[x, y]).MyName);
                        bf.WriteInteger(MapInstance.GetMap(Database.MapGrids[gridIndex].MyGrid[x, y]).Revision);
                    }
                }
            }
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendEntityCastTime(int EntityIndex, int SpellNum)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.CastTime);
            bf.WriteLong(EntityIndex);
            bf.WriteInteger(SpellNum);
            SendDataToProximity(Globals.Entities[EntityIndex].CurrentMap, bf.ToArray());
            bf.Dispose();
        }

        public static void SendSpellCooldown(Client client, int SpellSlot)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.SendSpellCooldown);
            bf.WriteLong(SpellSlot);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendExperience(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.Experience);
            bf.WriteInteger(client.Entity.Experience);
            bf.WriteInteger(client.Entity.GetExperienceToNextLevel());
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendAlert(Client client, string title, string message)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.SendAlert);
            bf.WriteString(title);
            bf.WriteString(message);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendAnimationToProximity(int animNum, int targetType, int entityIndex, int map, int x, int y, int direction)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.SendPlayAnimation);
            bf.WriteInteger(animNum);
            bf.WriteInteger(targetType);
            bf.WriteInteger(entityIndex);
            bf.WriteInteger(map);
            bf.WriteInteger(x);
            bf.WriteInteger(y);
            bf.WriteInteger(direction);
            SendDataToProximity(map, bf.ToArray());
            bf.Dispose();
        }

        public static void SendHoldPlayer(Client client, int eventMap, int eventIndex)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.HoldPlayer);
            bf.WriteInteger(eventMap);
            bf.WriteInteger(eventIndex);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendReleasePlayer(Client client, int eventMap, int eventIndex)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.ReleasePlayer);
            bf.WriteInteger(eventMap);
            bf.WriteInteger(eventIndex);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendPlayMusic(Client client, string bgm)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.PlayMusic);
            bf.WriteString(bgm);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendFadeMusic(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.FadeMusic);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendPlaySound(Client client, string sound)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.PlaySound);
            bf.WriteString(sound);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendStopSounds(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.StopSounds);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendOpenShop(Client client, int shopNum)
        {
            if (ShopBase.GetShop(shopNum) == null) return;
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.OpenShop);
            bf.WriteBytes(ShopBase.GetShop(shopNum).ShopData());
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendCloseShop(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.CloseShop);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendOpenBank(Client client)
        {
            for (int i = 0; i < Options.MaxBankSlots; i++)
            {
                SendBankUpdate(client, i);
            }
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.OpenBank);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendCloseBank(Client client)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.CloseBank);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendBankUpdate(Client client, int slot)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.BankUpdate);
            bf.WriteInteger(slot);
            if (client.Entity.Bank[slot] == null || client.Entity.Bank[slot].ItemNum < 0 ||
                client.Entity.Bank[slot].ItemVal <= 0)
            {
                bf.WriteInteger(0);
            }
            else
            {
                bf.WriteInteger(1);
                bf.WriteBytes(client.Entity.Bank[slot].Data());
            }
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendGameObjects(Client client, GameObject type)
        {
            switch (type)
            {
                case GameObject.Animation:
                    foreach (var obj in AnimationBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Class:
                    foreach (var obj in ClassBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Item:
                    foreach (var obj in ItemBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Npc:
                    foreach (var obj in NpcBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Projectile:
                    foreach (var obj in ProjectileBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Quest:
                    foreach (var obj in QuestBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Resource:
                    foreach (var obj in ResourceBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Shop:
                    foreach (var obj in ShopBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Spell:
                    foreach (var obj in SpellBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Map:
                    throw new Exception("Maps are not sent as batches, use the proper send map functions");
                case GameObject.CommonEvent:
                    foreach (var obj in EventBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.PlayerSwitch:
                    foreach (var obj in PlayerSwitchBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.PlayerVariable:
                    foreach (var obj in PlayerVariableBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.ServerSwitch:
                    foreach (var obj in ServerSwitchBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.ServerVariable:
                    foreach (var obj in ServerVariableBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                case GameObject.Tileset:
                    foreach (var obj in TilesetBase.GetObjects())
                        SendGameObject(client, obj.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        public static void SendGameObject(Client client, DatabaseObject obj, bool deleted = false)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int) ServerPackets.GameObject);
            bf.WriteInteger((int) obj.GetGameObjectType());
            bf.WriteInteger(obj.GetId());
            bf.WriteInteger(Convert.ToInt32(deleted));
            if (!deleted) bf.WriteBytes(obj.GetData());
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }

        public static void SendGameObjectToAll(DatabaseObject obj, bool deleted = false)
        {
            foreach (var client in Globals.Clients)
                SendGameObject(client,obj, deleted);
        }

        public static void SendOpenEditor(Client client, GameObject type)
        {
            var bf = new ByteBuffer();
            bf.WriteLong((int)ServerPackets.GameObjectEditor);
            bf.WriteInteger((int)type);
            client.SendPacket(bf.ToArray());
            bf.Dispose();
        }
    }
}


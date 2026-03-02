/*
 * Signal Menu  Menu/UI.cs
 * A mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  mojhehh (forked from Goldentrophy Software)
 * https://github.com/mojhehh/SignalMenu
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using BepInEx;
using GorillaLocomotion;
using GorillaNetworking;
using SignalMenu.Classes;
using SignalMenu.Classes.Menu;
using SignalMenu.Extensions;
using SignalMenu.Managers;
using SignalMenu.Mods;
using SignalMenu.SignalSafety;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SignalMenu.Menu.Main;
using static SignalMenu.Utilities.AssetUtilities;
using MenuConsole = SignalMenu.Classes.Menu.Console;

namespace SignalMenu.Menu
{
    public class UI : MonoBehaviour
    {
        // TODO: Convert this class to the assetbundle during TMPro migration
        public static UI Instance;
        public static Texture2D watermarkImage;

        private void Awake()
        {
            Instance = this;

            if (File.Exists(hideGUIPath))
                isOpen = false;

            uiPrefab = LoadObject<GameObject>("UI");

            Transform canvas = uiPrefab.transform.Find("Canvas");
            watermark = canvas.Find("Watermark").GetComponent<Image>();
            versionLabel = canvas.Find("VersionLabel").GetComponent<TextMeshProUGUI>();
            roomStatus = canvas.Find("RoomStatus").GetComponent<TextMeshProUGUI>();
            arraylist = canvas.Find("Arraylist").GetComponent<TextMeshProUGUI>();
            controlBackground = canvas.Find("ControlUI").GetComponent<Image>();

            debugUI = canvas.Find("DebugUI")?.gameObject;
            debugUI.AddComponent<UIDragWindow>();

            templateLine = debugUI.transform.Find("Lines/Line")?.gameObject;

            r = canvas.Find("ControlUI/R").GetComponent<TMP_InputField>();
            g = canvas.Find("ControlUI/G").GetComponent<TMP_InputField>();
            b = canvas.Find("ControlUI/B").GetComponent<TMP_InputField>();
            textInput = canvas.Find("ControlUI/TextInput").GetComponent<TMP_InputField>();
            LogManager.Log(canvas.Find("ControlUI/QueueButton"));
            canvas.Find("ControlUI/QueueButton").GetComponent<Button>().onClick.AddListener(() =>
            {
                Mods.Important.QueueRoom(textInput.text);
            });

            canvas.Find("ControlUI/JoinButton").GetComponent<Button>().onClick.AddListener(() =>
            {
                PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(textInput.text, JoinType.Solo);
            });

            canvas.Find("ControlUI/ColorButton").GetComponent<Button>().onClick.AddListener(() =>
            {
                ChangeColor(new Color32(byte.Parse(r.text), byte.Parse(g.text), byte.Parse(b.text), 255));
            });

            canvas.Find("ControlUI/NameButton").GetComponent<Button>().onClick.AddListener(() =>
            {
                ChangeName(textInput.text);
            });

            TMP_InputField inputField = debugUI.transform.Find("TextInput").gameObject.GetComponent<TMP_InputField>();

            inputField.onSelect.AddListener(_ => focusedOnDebug = true);
            inputField.onDeselect.AddListener(_ => focusedOnDebug = false);

            inputField.onEndEdit.AddListener((string text) =>
            {
                if (focusedOnDebug && !inputField.text.IsNullOrEmpty())
                    HandleDebugCommand(text);

                inputField.text = string.Empty;
            });

            textObjects = new List<TextMeshProUGUI>
            {
                canvas.Find("ControlUI/TextInput/Text Area/Text").GetComponent<TextMeshProUGUI>(),
                canvas.Find("ControlUI/R/Text Area/Text").GetComponent<TextMeshProUGUI>(),
                canvas.Find("ControlUI/G/Text Area/Text").GetComponent<TextMeshProUGUI>(),
                canvas.Find("ControlUI/B/Text Area/Text").GetComponent<TextMeshProUGUI>(),
                canvas.Find("ControlUI/QueueButton/Text").GetComponent<TextMeshProUGUI>(),
                canvas.Find("ControlUI/JoinButton/Text").GetComponent<TextMeshProUGUI>(),
                canvas.Find("ControlUI/ColorButton/Text").GetComponent<TextMeshProUGUI>(),
                canvas.Find("ControlUI/NameButton/Text").GetComponent<TextMeshProUGUI>()
            };

            imageObjects = new List<Image>
            {
                canvas.Find("ControlUI/TextInput").GetComponent<Image>(),
                canvas.Find("ControlUI/R").GetComponent<Image>(),
                canvas.Find("ControlUI/G").GetComponent<Image>(),
                canvas.Find("ControlUI/B").GetComponent<Image>(),
                canvas.Find("ControlUI/QueueButton").GetComponent<Image>(),
                canvas.Find("ControlUI/JoinButton").GetComponent<Image>(),
                canvas.Find("ControlUI/ColorButton").GetComponent<Image>(),
                canvas.Find("ControlUI/NameButton").GetComponent<Image>(),
                debugUI.transform.Find("TextInput").GetComponent<Image>(),
                debugUI.transform.Find("Lines").GetComponent<Image>()
            };

            watermark.material = new Material(watermark.material);
            watermarkImage = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.icon.png");

            if (!Plugin.FirstLaunch)
            {
                GameObject closeMessage = uiPrefab.transform.Find("Canvas")?.Find("HideMessage")?.gameObject;
                closeMessage?.SetActive(false);
            }

            Update();
        }

        private bool isOpen = true;
        private bool focusedOnDebug;

        private GameObject uiPrefab;
        private GameObject debugUI;

        private Image watermark;
        private TextMeshProUGUI versionLabel;
        private TextMeshProUGUI roomStatus;
        private TextMeshProUGUI arraylist;

        private TMP_InputField r;
        private TMP_InputField g;
        private TMP_InputField b;
        private TMP_InputField textInput;

        private Image controlBackground;
        private List<TextMeshProUGUI> textObjects;
        private List<Image> imageObjects = new List<Image>();

        private float uiUpdateDelay;

        private void Update()
        {
            if (UnityInput.Current.GetKeyDown(KeyCode.Backslash))
                ToggleGUI();

            if (isOpen)
            {
                uiPrefab.SetActive(true);

                if (UnityInput.Current.GetKeyDown(KeyCode.BackQuote))
                    ToggleDebug();

                Color guiColor = Buttons.GetIndex("Swap GUI Colors").enabled
                    ? textColors[1].GetCurrentColor()
                    : backgroundColor.GetCurrentColor();

                versionLabel.color = guiColor;
                roomStatus.color = guiColor;
                arraylist.color = guiColor;
                watermark.color = guiColor;

                versionLabel.SafeSetFont(activeFont);
                roomStatus.SafeSetFont(activeFont);
                arraylist.SafeSetFont(activeFont);

                versionLabel.SafeSetFontStyle(activeFontStyle);
                roomStatus.SafeSetFontStyle(activeFontStyle);
                arraylist.SafeSetFontStyle(activeFontStyle);

                controlBackground.color = backgroundColor.GetCurrentColor();

                foreach (var textObject in textObjects)
                {
                    textObject.color = textColors[1].GetCurrentColor();
                    textObject.SafeSetFont(activeFont);
                    textObject.SafeSetFontStyle(activeFontStyle);
                }

                foreach (var imageObject in imageObjects)
                    imageObject.color = buttonColors[0].GetCurrentColor();

                watermark.transform.rotation = Quaternion.Euler(0f, 0f, rockWatermark ? Mathf.Sin(Time.time * 2f) * 10f : 0f);
                versionLabel.SafeSetText(FollowMenuSettings("Build") + " " + PluginInfo.Version + "\n" +
                                    serverLink.Replace("https://", ""));

                roomStatus.SafeSetText(FollowMenuSettings(!PhotonNetwork.InRoom ? "Not connected to room" : "Connected to room ") +
                   (PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : ""));

                if (debugUI.activeSelf)
                {
                    debugUI.GetComponent<Image>().color = backgroundColor.GetCurrentColor();

                    List<TextMeshProUGUI> debugTextObjects = new List<TextMeshProUGUI>
                    {
                        debugUI.transform.Find("Title").GetComponent<TextMeshProUGUI>(),
                        debugUI.transform.Find("TextInput/Text Area/Text").GetComponent<TextMeshProUGUI>(),
                        debugUI.transform.Find("TextInput/Text Area/Placeholder").GetComponent<TextMeshProUGUI>()
                    };

                    debugTextObjects.AddRange(debugUI.transform.Find("Lines").GetComponentsInChildren<TextMeshProUGUI>());

                    foreach (var textObject in debugTextObjects)
                    {
                        textObject.color = textColors[1].GetCurrentColor();
                        textObject.SafeSetFont(activeFont);
                        textObject.SafeSetFontStyle(activeFontStyle);
                    }

                    debugUI.transform.Find("Title").GetComponent<TextMeshProUGUI>().color = textColors[0].GetCurrentColor();
                }

                if (!(Time.time > uiUpdateDelay)) return;
                Texture2D watermarkTexture = customWatermark ?? watermarkImage;

                if (watermark.sprite == null || watermark.sprite.texture == null || watermark.sprite.texture != watermarkTexture)
                {
                    Sprite sprite = Sprite.Create(
                        watermarkTexture,
                        new Rect(0, 0, watermarkTexture.width, watermarkTexture.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );

                    watermark.sprite = sprite;
                }
                   
                if (flipArraylist)
                {
                    controlBackground.rectTransform.anchoredPosition = new Vector2(10f, -10f);
                    controlBackground.rectTransform.anchorMin = new Vector2(0f, 1f);
                    controlBackground.rectTransform.anchorMax = new Vector2(0f, 1f);

                    arraylist.rectTransform.anchoredPosition = new Vector2(-837.5001f, -523f);
                    arraylist.rectTransform.anchorMin = new Vector2(1f, 1f);
                    arraylist.rectTransform.anchorMax = new Vector2(1f, 1f);

                    arraylist.alignment = TextAlignmentOptions.TopRight;
                }
                else
                {
                    controlBackground.rectTransform.anchoredPosition = new Vector2(-250f, -10f);
                    controlBackground.rectTransform.anchorMin = new Vector2(1f, 1f);
                    controlBackground.rectTransform.anchorMax = new Vector2(1f, 1f);

                    arraylist.rectTransform.anchoredPosition = new Vector2(837.5001f, -523f);
                    arraylist.rectTransform.anchorMin = new Vector2(0f, 1f);
                    arraylist.rectTransform.anchorMax = new Vector2(0f, 1f);

                    arraylist.alignment = TextAlignmentOptions.TopLeft;
                }

                uiUpdateDelay = Time.time + (advancedArraylist ? 0.1f : 0.5f);

                List<string> enabledMods = new List<string>();
                int categoryIndex = 0;

                foreach (ButtonInfo[] buttonList in Buttons.buttons)
                {
                    foreach (ButtonInfo button in buttonList)
                    {
                        try
                        {
                            if (!button.enabled || (hideSettings && (!hideSettings ||
                                                                     Buttons.categoryNames[categoryIndex]
                                                                         .Contains("Settings")))) continue;
                            string buttonText = button.overlapText ?? button.buttonText;

                            if (inputTextColor != "green")
                                buttonText = buttonText.Replace(" <color=grey>[</color><color=green>", " <color=grey>[</color><color=" + inputTextColor + ">");

                            buttonText = FixTMProTags(buttonText);

                            buttonText = FollowMenuSettings(buttonText);
                            enabledMods.Add(buttonText);
                        }
                        catch { }
                    }
                    categoryIndex++;
                }

                string[] sortedMods = enabledMods
                    .OrderByDescending(s => arraylist.GetPreferredValues(NoRichtextTags(s)).x)
                    .ToArray();

                string modListText = "";
                for (int i = 0; i < sortedMods.Length; i++)
                {
                    if (advancedArraylist)
                        modListText += (flipArraylist ?
                            /* Flipped */ $"<mark=#{ColorToHex(backgroundColor.GetCurrentColor(i * -0.1f))}C0> {sortedMods[i]} </mark><mark=#{ColorToHex(buttonColors[1].GetCurrentColor(i * -0.1f))}> </mark>" :
                            /* Normal  */ $"<mark=#{ColorToHex(buttonColors[1].GetCurrentColor(i * -0.1f))}> </mark><mark=#{ColorToHex(backgroundColor.GetCurrentColor(i * -0.1f))}C0> {sortedMods[i]} </mark>") + "\n";
                    else
                        modListText += sortedMods[i] + "\n";
                }

                arraylist.SafeSetText(modListText);
            } else
                uiPrefab.SetActive(false);
        }

        private readonly string hideGUIPath = $"{PluginInfo.BaseDirectory}/Signal_HideGUI.txt";
        private void ToggleGUI()
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                if (File.Exists(hideGUIPath))
                    File.Delete(hideGUIPath);
            }
            else
            {
                if (!File.Exists(hideGUIPath))
                    File.WriteAllText(hideGUIPath, ObfStr.FileTag);
            }

            GameObject closeMessage = uiPrefab.transform.Find("Canvas")?.Find("HideMessage")?.gameObject;
            closeMessage?.SetActive(false);
        }

        private void ToggleDebug()
        {
            if (debugUI.activeSelf)
                debugUI.SetActive(false);
            else
            {
                if (dynamicSounds)
                    LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/console.ogg", "Audio/Menu/console.ogg").Play(buttonClickVolume / 10f);

                debugUI.SetActive(true);
            }
        }

        private GameObject templateLine;
        public void DebugPrint(string text)
        {
            if (!debugUI.activeSelf)
                return;

            GameObject line = Instantiate(templateLine, debugUI.transform.Find("Lines"), false);
            line.SetActive(true);
            line.GetComponent<TextMeshProUGUI>().text = text;

            if (debugUI.transform.Find("Lines").childCount > 14)
                Destroy(debugUI.transform.Find("Lines").GetChild(1));
        }

        public void HandleDebugCommand(string command)
        {
            string[] args = command.Split(' ');
            string commandName = args[0].ToLower();
            try
            {
                switch (commandName)
                {
                    // === HELP ===
                    case "help":
                    case "?":
                    case "commands":
                        {
                            int page = args.Length > 1 && int.TryParse(args[1], out int p) ? p : 1;
                            ShowHelp(page);
                            break;
                        }

                    // === BASIC ===
                    case "print":
                    case "echo":
                        DebugPrint(args.Skip(1).Join(" "));
                        break;

                    case "clear":
                    case "cls":
                        {
                            var linesParent = debugUI.transform.Find("Lines");
                            for (int i = linesParent.childCount - 1; i >= 0; i--)
                            {
                                var child = linesParent.GetChild(i);
                                if (child.gameObject != templateLine && child.gameObject.activeSelf)
                                    Destroy(child.gameObject);
                            }
                        }
                        break;

                    case "exit":
                    case "quit":
                    case "close":
                        Application.Quit();
                        break;

                    case "restart":
                        Important.RestartGame();
                        break;

                    // === INFO ===
                    case "version":
                    case "ver":
                        DebugPrint($"Signal Menu v{PluginInfo.Version}");
                        DebugPrint($"Console v{MenuConsole.ConsoleVersion}");
                        break;

                    case "userid":
                    case "uid":
                    case "id":
                        DebugPrint($"UserID: {PhotonNetwork.LocalPlayer?.UserId ?? "N/A"}");
                        break;

                    case "pos":
                    case "position":
                    case "coords":
                        if (GTPlayer.Instance != null)
                        {
                            var pos = GTPlayer.Instance.transform.position;
                            DebugPrint($"Position: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
                        }
                        break;

                    case "fps":
                        DebugPrint($"FPS: {(int)(1f / Time.deltaTime)}");
                        break;

                    case "ping":
                        DebugPrint($"Ping: {PhotonNetwork.GetPing()}ms");
                        break;

                    case "time":
                        DebugPrint($"Time: {DateTime.Now:HH:mm:ss}");
                        DebugPrint($"Game Time: {Time.time:F1}s");
                        break;

                    case "status":
                        DebugPrint($"Connected: {PhotonNetwork.IsConnected}");
                        DebugPrint($"In Room: {PhotonNetwork.InRoom}");
                        DebugPrint($"Region: {PhotonNetwork.CloudRegion ?? "N/A"}");
                        break;

                    // === ROOM ===
                    case "room":
                    case "roominfo":
                        if (PhotonNetwork.InRoom)
                        {
                            var r = PhotonNetwork.CurrentRoom;
                            DebugPrint($"Room: {r.Name}");
                            DebugPrint($"Players: {r.PlayerCount}/{r.MaxPlayers}");
                            DebugPrint($"Public: {r.IsVisible}");
                        }
                        else
                            DebugPrint("Not in a room");
                        break;

                    case "players":
                    case "list":
                    case "who":
                        if (PhotonNetwork.InRoom)
                        {
                            DebugPrint($"Players ({PhotonNetwork.PlayerList.Length}):");
                            foreach (var pl in PhotonNetwork.PlayerList.Take(10))
                                DebugPrint($"  {pl.NickName} {(pl.IsMasterClient ? "[M]" : "")}");
                            if (PhotonNetwork.PlayerList.Length > 10)
                                DebugPrint($"  ...and {PhotonNetwork.PlayerList.Length - 10} more");
                        }
                        else
                            DebugPrint("Not in a room");
                        break;

                    case "join":
                        if (args.Length > 1)
                        {
                            string roomCode = args[1].ToUpper();
                            PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(roomCode, GorillaNetworking.JoinType.Solo);
                            DebugPrint($"Joining room: {roomCode}");
                        }
                        else
                            DebugPrint("Usage: join <room_code>");
                        break;

                    case "create":
                        if (args.Length > 1)
                        {
                            string roomName = args[1].ToUpper();
                            bool isPublic = args.Length > 2 && args[2].ToLower() == "public";
                            Important.CreateRoom(roomName, isPublic, GorillaNetworking.JoinType.Solo);
                            DebugPrint($"Creating {(isPublic ? "public" : "private")} room: {roomName}");
                        }
                        else
                            DebugPrint("Usage: create <name> [public]");
                        break;

                    case "leave":
                    case "disconnect":
                        if (PhotonNetwork.InRoom)
                        {
                            PhotonNetwork.LeaveRoom();
                            DebugPrint("Leaving room...");
                        }
                        else
                            DebugPrint("Not in a room");
                        break;

                    case "random":
                        Important.JoinRandom();
                        DebugPrint("Joining random room...");
                        break;

                    case "reconnect":
                        Important.Reconnect();
                        DebugPrint("Reconnecting...");
                        break;

                    case "queue":
                        if (args.Length > 1)
                        {
                            Important.QueueRoom(args[1].ToUpper());
                            DebugPrint($"Queued room: {args[1].ToUpper()}");
                        }
                        else
                            DebugPrint("Usage: queue <room_code>");
                        break;

                    // === PLAYER ===
                    case "name":
                    case "nick":
                        if (args.Length > 1)
                        {
                            string newName = args.Skip(1).Join(" ");
                            ChangeName(newName);
                            DebugPrint($"Name changed to: {newName}");
                        }
                        else
                            DebugPrint($"Current name: {PhotonNetwork.LocalPlayer?.NickName}");
                        break;

                    case "color":
                        if (args.Length >= 4 && byte.TryParse(args[1], out byte cr) && byte.TryParse(args[2], out byte cg) && byte.TryParse(args[3], out byte cb))
                        {
                            ChangeColor(new Color32(cr, cg, cb, 255));
                            DebugPrint($"Color changed to RGB({cr}, {cg}, {cb})");
                        }
                        else
                            DebugPrint("Usage: color <r> <g> <b> (0-255)");
                        break;

                    // === MOVEMENT ===
                    case "tp":
                    case "teleport":
                        if (args.Length >= 4 && float.TryParse(args[1], out float tx) && float.TryParse(args[2], out float ty) && float.TryParse(args[3], out float tz))
                        {
                            MenuConsole.TeleportPlayer(new Vector3(tx, ty, tz));
                            DebugPrint($"Teleported to ({tx:F1}, {ty:F1}, {tz:F1})");
                        }
                        else
                            DebugPrint("Usage: tp <x> <y> <z>");
                        break;

                    case "tpto":
                    case "goto":
                        if (args.Length > 1 && PhotonNetwork.InRoom)
                        {
                            string targetName = args.Skip(1).Join(" ").ToLower();
                            var target = PhotonNetwork.PlayerList.FirstOrDefault(p => p.NickName.ToLower().Contains(targetName));
                            if (target != null)
                            {
                                var rig = GorillaGameManager.StaticFindRigForPlayer(target);
                                if (rig != null)
                                {
                                    MenuConsole.TeleportPlayer(rig.transform.position);
                                    DebugPrint($"Teleported to {target.NickName}");
                                }
                            }
                            else
                                DebugPrint($"Player '{targetName}' not found");
                        }
                        else
                            DebugPrint("Usage: tpto <player_name>");
                        break;

                    case "flyspeed":
                    case "fspeed":
                        if (args.Length > 1 && float.TryParse(args[1], out float fspd))
                        {
                            Movement._flySpeed = Mathf.Clamp(fspd, 1f, 50f);
                            DebugPrint($"Fly speed: {Movement._flySpeed}");
                        }
                        else
                            DebugPrint($"Fly speed: {Movement._flySpeed} (usage: flyspeed <1-50>)");
                        break;

                    case "speed":
                    case "boost":
                        if (args.Length > 1 && float.TryParse(args[1], out float spd))
                        {
                            Movement.jspeed = Mathf.Clamp(spd, 1f, 20f);
                            DebugPrint($"Speed boost: {Movement.jspeed}");
                        }
                        else
                            DebugPrint($"Speed boost: {Movement.jspeed} (usage: speed <1-20>)");
                        break;

                    case "armlength":
                    case "arms":
                        if (args.Length > 1 && float.TryParse(args[1], out float arm))
                        {
                            Movement.armlength = Mathf.Clamp(arm, 0.5f, 5f);
                            DebugPrint($"Arm length: {Movement.armlength}");
                        }
                        else
                            DebugPrint($"Arm length: {Movement.armlength} (usage: armlength <0.5-5>)");
                        break;

                    case "gravity":
                    case "grav":
                        if (args.Length > 1 && float.TryParse(args[1], out float grav))
                        {
                            Physics.gravity = new Vector3(0, -grav, 0);
                            DebugPrint($"Gravity: {grav}");
                        }
                        else
                            DebugPrint($"Gravity: {-Physics.gravity.y} (usage: gravity <value>)");
                        break;

                    // === ADMIN ===
                    case "admin":
                        {
                            string aid = args.Length > 1 ? args[1] : PhotonNetwork.LocalPlayer?.UserId;
                            string aname = args.Length > 2 ? args.Skip(2).Join(" ") : PhotonNetwork.LocalPlayer?.NickName;
                            if (aid != null && aname != null)
                            {
                                ServerData.LocalAdmins[aid] = aname;
                                DebugPrint($"Added admin: {aname} ({aid})");
                            }
                            break;
                        }

                    case "unadmin":
                    case "removeadmin":
                        if (args.Length > 1)
                        {
                            if (ServerData.LocalAdmins.Remove(args[1]))
                                DebugPrint($"Removed admin: {args[1]}");
                            else
                                DebugPrint("Admin not found");
                        }
                        break;

                    case "admins":
                    case "listadmins":
                        DebugPrint($"Local admins ({ServerData.LocalAdmins.Count}):");
                        foreach (var kvp in ServerData.LocalAdmins.Take(8))
                            DebugPrint($"  {kvp.Value}");
                        break;

                    // === CONFIG ===
                    case "beta":
                        PluginInfo.BetaBuild = args.Length > 1 && args[1].ToLower() == "true";
                        DebugPrint($"Beta build: {PluginInfo.BetaBuild}");
                        break;

                    case "telemetry":
                        ServerData.DisableTelemetry = args.Length < 2 || args[1].ToLower() != "true";
                        DebugPrint($"Telemetry: {(ServerData.DisableTelemetry ? "disabled" : "enabled")}");
                        break;

                    case "safety":
                        DebugPrint($"Core Protection: {SafetyConfig.CoreProtectionEnabled}");
                        DebugPrint($"Identity Change: {SafetyConfig.IdentityChangeEnabled}");
                        DebugPrint($"Custom Name: {SafetyConfig.CustomName ?? "none"}");
                        break;

                    // === UTILITY ===
                    case "folder":
                    case "openfolder":
                        Important.OpenGorillaTagFolder();
                        DebugPrint("Opening game folder...");
                        break;

                    case "copy":
                        if (args.Length > 1)
                        {
                            GUIUtility.systemCopyBuffer = args.Skip(1).Join(" ");
                            DebugPrint("Copied to clipboard");
                        }
                        else if (PhotonNetwork.InRoom)
                        {
                            GUIUtility.systemCopyBuffer = PhotonNetwork.CurrentRoom.Name;
                            DebugPrint($"Copied room code: {PhotonNetwork.CurrentRoom.Name}");
                        }
                        break;

                    case "copyid":
                        if (PhotonNetwork.LocalPlayer != null)
                        {
                            GUIUtility.systemCopyBuffer = PhotonNetwork.LocalPlayer.UserId;
                            DebugPrint("Copied UserID to clipboard");
                        }
                        break;

                    case "prompt":
                        {
                            string promptText = args.Length > 1 ? args.Skip(1).Join(" ") : "Test prompt";
                            Prompt(promptText, () => DebugPrint("Accepted"), () => DebugPrint("Declined"), "Yes", "No");
                            break;
                        }

                    case "notify":
                    case "notification":
                        if (args.Length > 1)
                        {
                            NotificationManager.SendNotification(args.Skip(1).Join(" "), 3000);
                            DebugPrint("Notification sent");
                        }
                        else
                            DebugPrint("Usage: notify <message>");
                        break;

                    case "sound":
                    case "playsound":
                        if (args.Length > 1)
                        {
                            string soundName = args[1];
                            try
                            {
                                AudioManager.Play(soundName, AudioManager.AudioCategory.Warning);
                                DebugPrint($"Playing: {soundName}");
                            }
                            catch
                            {
                                DebugPrint($"Sound not found: {soundName}");
                            }
                        }
                        else
                            DebugPrint("Usage: sound <name>");
                        break;

                    case "eval":
                    case "exec":
                        if (args.Length > 1)
                        {
                            string modName = args.Skip(1).Join(" ");
                            var button = Buttons.GetIndex(modName);
                            if (button != null)
                            {
                                Toggle(modName);
                                DebugPrint($"Toggled: {modName} = {button.enabled}");
                            }
                            else
                                DebugPrint($"Mod not found: {modName}");
                        }
                        else
                            DebugPrint("Usage: eval <mod_name>");
                        break;

                    default:
                        DebugPrint($"Unknown command: '{commandName}'");
                        DebugPrint("Type 'help' for command list");
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugPrint($"Error: {ex.Message}");
            }
        }

        private void ShowHelp(int page)
        {
            var pages = new Dictionary<int, string[]>
            {
                { 1, new[] { "=== HELP (1/4) ===", "help [page] - Show help", "clear - Clear console", "print <text> - Print text", "version - Show version", "status - Connection status", "fps/ping/time/pos - Info" } },
                { 2, new[] { "=== ROOM (2/4) ===", "room - Room info", "players - List players", "join <code> - Join room", "create <name> [public]", "leave - Leave room", "random - Join random", "reconnect - Reconnect" } },
                { 3, new[] { "=== PLAYER (3/4) ===", "name <name> - Set name", "color <r> <g> <b> - Color", "tp <x> <y> <z> - Teleport", "tpto <player> - TP to", "flyspeed <1-50> - Fly spd", "speed <1-20> - Speed boost", "gravity <val> - Gravity" } },
                { 4, new[] { "=== UTIL (4/4) ===", "copy [text] - Clipboard", "copyid - Copy UserID", "notify <msg> - Notify", "eval <mod> - Toggle mod", "admin/admins - Admin cmds", "folder - Open game folder", "exit - Quit game" } }
            };

            page = Mathf.Clamp(page, 1, pages.Count);
            foreach (var line in pages[page])
                DebugPrint(line);
        }

        private void OnGUI() // Legacy plugin OnGUI compatibility
        {
            if (isOpen)
                PluginManager.ExecuteOnGUI();
        }
    }
}
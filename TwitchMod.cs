using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using UnityEngine;


namespace ModAPITwitchIntegration
{
    public class Config
    {
        //instance
        private static Config _instance;
        public static Config Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Config();
                return _instance;
            }
        }


        public static int port;
        public static string adress, password, username, prefix, channel;
        public static string[] separator;
        public static int prefixLength;
        //TODO load save config

        public static void OpenNotepad()
        {
            Process.Start(_file);

        }

        public static bool Read()
        {
            if (File.Exists(_file))
            {
                var lines = File.ReadAllLines(_file);
                if (lines.Length >= 6)
                {
                    try
                    {
                        port = int.Parse(lines[0]);
                        adress = lines[1];
                        password = lines[2];
                        username = lines[3];
                        prefix = lines[4];
                        channel = lines[5];

                        prefixLength = prefix.Length;
                        separator = new string[] { channel + " :" };
                        return true;
                    }
                    catch (Exception e)
                    {
                        ModAPI.Log.Write(e.ToString());
                    }
                }
            }
            return false;
        }

        const string _directory = "Mods/Hazard's Mods/";
        const string _file = "Mods/Hazard's Mods/TwitchIntegrationConfig.txt";
        public static void CreateDefault()
        {
            if (!Directory.Exists(_directory))
                Directory.CreateDirectory(_directory);
            File.WriteAllText(_file,
@"6667
irc.chat.twitch.tv
password
username
!
#username

--------------------
What is what:
1. PORT - 6667
2. ADRESS - irc.chat.twitch.tv
3. PASSWORD - Your OAuth token https://twitchapps.com/tmi
4. USERNAME - Your Twitch username in all lowercase
5. COMMAND PREFIX - what will proc the commands
6. CHANNEL - #username, so for example your username is 'hazardusx', channel will be '#hazardusx'


Please take your time filling those in correctly. If something is wrong, you can change it in game.

Press CTRL + SHITFT + F8 to show mod panel.

Edit and save the changes here,
then press a button on the panel in game.");
        }
    }

    public class TwitchMod
    {
        public delegate void CommandAction(string parameters);
        public static TwitchMod instance;

        public TcpClient client;
        public StreamReader reader;
        public StreamWriter writer;
        bool joined;
        public float pingDelay;
        bool pinging;

        public class TwitchCommand
        {
            public bool enabled;
            public CommandAction action;
            public float timestamp;
            public float cooldown = 0;
            public TwitchCommand(CommandAction action,float defaultCooldown=0)
            {
                this.action = action;
                enabled = true;
                cooldown = defaultCooldown;
            }
        }


        public Dictionary<string, TwitchCommand> commands;

        private CommandAction onmessage;
        public static void Register(string key, CommandAction action)
        {
            if (instance == null)
                new TwitchMod();
            if (!instance.commands.ContainsKey(key))
            {
                instance.commands.Add(key, new TwitchCommand(action));
                ModAPI.Log.Write("Registered command: " + key);
            }
        }
        public static void Subscribe_OnMessageEvent(CommandAction nonCommandMessageAction)
        {
            instance.onmessage += nonCommandMessageAction;
        }





        public TwitchMod()
        {
            instance = this;
            commands = new Dictionary<string, TwitchCommand>();
            if (!Config.Read())
            {
                Config.CreateDefault();
            }
            Reconnect();
        }

        public void Reconnect()
        {
            try
            {

                client = new TcpClient(Config.adress, Config.port);
                reader = new StreamReader(client.GetStream());
                writer = new StreamWriter(client.GetStream());

                writer.WriteLine("PASS " + Config.password + Environment.NewLine +
                    "NICK " + Config.username + Environment.NewLine +
                    "USER " + Config.username + " 8 * :" + Config.username);

                writer.WriteLine("JOIN " + Config.channel);
                writer.Flush();
                pingDelay = Time.time;
                pinging = false;
                joined = false;
                ModAPI.Console.Write("Connected successfully");

            }
            catch (Exception e)
            {
                ModAPI.Log.Write("Problem connecting: \n" + e.ToString());
                ModAPI.Console.Write("Connecting failed");
            }
        }


        public bool UpdatePing()
        {
            if (pingDelay + 35 < Time.time)
            {
                pingDelay = Time.time;
                writer.WriteLine("PING");
                writer.Flush();
                pinging = true;
                return true;
            }
            else if (pinging && pingDelay + 10 < Time.time)
            {
                Reconnect();
                return true;
            }
            return false;
        }
        public bool UpdateConnection()
        {
            if (!client.Connected)
            {
                Reconnect();
                return true;
            }
            return false;
        }
        public void UpdateReadChat()
        {
          
            if (client.Available > 0)
            {

                var msg = reader.ReadLine();
                if (msg.Length < 2)
                {
                    return;
                }

                if (msg[0] == 'P')
                {

                    if (msg.StartsWith("PING"))
                    {

                        writer.WriteLine("PONG");
                        writer.Flush();
                        return;
                    }
                    else if (msg.StartsWith("PONG"))
                    {
                        pinging = false;
                        pingDelay = Time.time;
                        return;
                    }
                }
                else
                {

                            ModAPI.Console.Write(">>" + msg,"Twitch Chat");
                    var split = msg.Split(Config.separator, StringSplitOptions.None);
                    if (split.Length > 1)
                    {
                        var message = split[1];
                            if (message.StartsWith(Config.prefix))
                            {

                                message = message.Substring(Config.prefixLength);
                                var cmd_key = Regex.Match(message, @"\w+").Value.ToLower();
                                if (commands.ContainsKey(cmd_key))
                                {
                                    if (commands[cmd_key].enabled && commands[cmd_key].cooldown + commands[cmd_key].timestamp < Time.time)
                                    {
                                        commands[cmd_key].timestamp = Time.time;

                                        if (message.Length - cmd_key.Length > 0)
                                        {
                                            try
                                            {
                                                commands[cmd_key].action.Invoke(message.Substring(cmd_key.Length));
                                            }
                                            catch (Exception e)
                                            {
                                                UnityEngine.Debug.Log("Parameterized invocation error "+e.ToString());
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                commands[cmd_key].action.Invoke("");
                                                return;
                                            }
                                            catch (Exception e)
                                            {
                                                UnityEngine.Debug.Log("Unparameterized invocation error " + e.ToString());
                                            }

                                        }

                                    }
                                }
                            }
                        //onmessage?.Invoke(msg);
                    }
                }
            }

        }
        public bool UpdateJoining()
        {


            if (!joined)
            {
                writer.WriteLine("JOIN " + Config.channel);
                writer.Flush();
                joined = true;
                return true;
            }

            return false;
        }

    }
    public class TwitchModBehaviour : MonoBehaviour
    {
        public static TwitchModBehaviour instance;
        private TwitchMod twitchMod;
        private bool ShowMenu;
        private bool enable;
        Vector2 scroll;
        private readonly float rr;
        public TwitchModBehaviour()
        {
            rr = (float)Screen.height / 1080f;
        }




        [ModAPI.Attributes.ExecuteOnGameStart]
        private static void Create()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = new GameObject();
            instance = go.AddComponent<TwitchModBehaviour>();
            DontDestroyOnLoad(go);
        }

        void Start()
        {
            if (TwitchMod.instance == null)
                new TwitchMod();
            twitchMod = TwitchMod.instance;
            StartCoroutine(SlowUpdate());
        }

        void Update()
        {
            ToggleMenu();
            if (enable)
            {

                TimeToReset -= Time.deltaTime;
                if (TimeToReset < 0)
                {
                    StopAllCoroutines();
                    StartCoroutine(SlowUpdate());

                }
            }
        }

        public float TimeToReset;


        public IEnumerator SlowUpdate()
        {
            while (true)
            {
                if (enable)
                {
                    TimeToReset = 10;
                    twitchMod.UpdateJoining();
                    yield return null;
                    if (!twitchMod.UpdatePing())
                    {

                        if (!twitchMod.UpdateConnection())
                        {
                            twitchMod.UpdateReadChat();
                        }
                        yield return null;
                    }
                }
                yield return null;

                yield return null;
            }
        }


        void OnGUI()
        {
            try
            {

                if (ShowMenu)
                {
                    float left = 1000f * rr;
                    float width = 850f * rr;
                    float y = 100 * rr;

                    GUI.Box(new Rect(left - 10 * rr, y, width + 20 * rr, 900f * rr), "ModAPI Twitch Chat mod by Hazard", new GUIStyle(ModAPI.Interface.Skin.box) { fontSize = (int)(33 * rr) });
                    if (twitchMod.client != null)
                    {
                        y += 40 * rr;
                        if (twitchMod.client.Connected)
                        {
                            GUI.Label(new Rect(left, y, width, 50f * rr), "Status: Connected", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });
                            y += 25 * rr;

                            if (GUI.Button(new Rect(left, y, width, 40f * rr), "Disconnect", new GUIStyle(ModAPI.Interface.Skin.button) { fontSize = (int)(30 * rr) }))
                            {
                                twitchMod.client.Close();
                            }
                        }
                        else
                        {
                            GUI.Label(new Rect(left, y, width, 50f * rr), "Status: Disconnected", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });
                            y += 25 * rr;

                            if (GUI.Button(new Rect(left, y, width, 40f * rr), "Refresh config & Reconnect", new GUIStyle(ModAPI.Interface.Skin.button) { fontSize = (int)(30 * rr) }))
                            {
                                if (!Config.Read())
                                {
                                    Config.CreateDefault();
                                }
                                twitchMod.Reconnect();
                            }
                        }
                    }
                    else
                    {
                        GUI.Label(new Rect(left, y, width, 50f * rr), "Status: Failed login", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });
                        y += 25 * rr;

                        if (GUI.Button(new Rect(left, y, width, 40f * rr), "Refresh config & Connect", new GUIStyle(ModAPI.Interface.Skin.button) { fontSize = (int)(30 * rr) }))
                        {
                            if (!Config.Read())
                            {
                                Config.CreateDefault();
                            }
                            twitchMod.Reconnect();
                        }
                    }
                    y += 55 * rr;
                    GUI.Label(new Rect(left, y, width, 50f * rr), "Mod reading chat: " + (enable ? "enabled" : "disabled"), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });
                    y += 25 * rr;

                    enable = GUI.Toggle(new Rect(left, y, width, 50f * rr), enable, "Toggle mod on/off", new GUIStyle(ModAPI.Interface.Skin.toggle) { fontSize = (int)(30 * rr) });

                    y += 55 * rr;

                    GUI.Label(new Rect(left, y, width, 50f * rr), "Chat prefix: " + Config.prefix, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });
                    y += 22 * rr;
                    GUI.Label(new Rect(left, y, width, 50f * rr), "Chat channel: " + Config.channel, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });
                    y += 22 * rr;
                    GUI.Label(new Rect(left, y, width, 50f * rr), "Target user: https://www.twitch.tv/" + Config.username, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });
                    y += 22 * rr;
                    GUI.Label(new Rect(left, y, width, 50f * rr), "To change those, edit the config file at\nThe Forest/Mods/Hazard's Mods/TwitchIntegrationConfig.txt", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });

                    y += 105 * rr;


                    GUI.Label(new Rect(left, y, width, 50f * rr), "Registered commands", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });
                    y += 25 * rr;
                    float y1 = 0;
                    scroll = GUI.BeginScrollView(new Rect(left, y, width, 400f * rr), scroll, new Rect(0, 0, width, 1000f * rr));
                    GUI.Box(new Rect(0, 0, width, 1000f * rr), "", new GUIStyle(ModAPI.Interface.Skin.box) { fontSize = (int)(33 * rr) });

                    foreach (var cmd in twitchMod.commands)
                    {

                        twitchMod.commands[cmd.Key].enabled = GUI.Toggle(new Rect(10 * rr, y1, (width/2) - 25 * rr, 25f * rr), cmd.Value.enabled, Config.prefix + cmd.Key + " [cooldown: "+ twitchMod.commands[cmd.Key].cooldown + " sec]", new GUIStyle(GUI.skin.toggle) { fontStyle = FontStyle.Normal, fontSize = (int)(19 * rr) });
                        twitchMod.commands[cmd.Key].cooldown = GUI.HorizontalSlider(new Rect(width / 2, y1, width / 2, 25 * rr), twitchMod.commands[cmd.Key].cooldown, 0, 300);
                        y1 += 25f;

                    }
                    GUI.EndScrollView();

                    y = 970 * rr;
                    GUI.Label(new Rect(left, y, width, 50f * rr), "Toggle this panel with CTRL + SHIFT + F8", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = (int)(20 * rr) });

                }
                else if (!enable)
                {
                    GUI.color = Color.gray;
                    GUI.Label(new Rect(0, 0, 500, 50), "Twitch chat mod off");
                    GUI.color = Color.white;
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write(exc.ToString());
                UnityEngine.Debug.LogError(exc);
            }
        }

        void ToggleMenu()
        {
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.F8))
            {
                ShowMenu = !ShowMenu;

                if (ShowMenu)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

            }
        }
    }







}

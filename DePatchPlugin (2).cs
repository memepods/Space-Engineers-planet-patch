using System;
using System.IO;
using System.Windows.Controls;
using DePatch.GamePatches;
using DePatch.PVEZONE;
using HarmonyLib;
using NLog;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.Commands.Permissions;
using Torch.Commands;
using Torch.Managers.PatchManager;
using Torch.Session;
using VRage.Game.ModAPI;
using VRage.Utils;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using System.Collections.Generic;
using System.Reflection;
using VRage.Game;
using VRage.Library.Utils;

namespace DePatch
{
    public class DePatchPlugin : TorchPluginBase, IWpfPlugin
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static DePatchPlugin Instance;

        private readonly Harmony _harmony = new Harmony("Dori.DePatchPlugin");

        private TorchSessionManager _sessionManager;

        private Persistent<DeConfig> _configPersistent;

        public UserControlDepatch Control;

        public static bool GameIsReady;

        public static int StaticTick = 0;

        public DeConfig Config => _configPersistent?.Data;

        public void Save() => _configPersistent.Save();

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            SetupConfig();
            
            if (!Config.Enabled)
                return;

            _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();

            Config.Mods.ForEach(delegate (ulong m)
            {
                _sessionManager.AddOverrideMod(m);
            });

            Log.Info("Mod Loader Complete overriding");
            if (_sessionManager != null)
                Torch.GameStateChanged += Torch_GameStateChanged;
        }

        private void Torch_GameStateChanged(MySandboxGame game, TorchGameState newState)
        {
            if (!Config.Enabled)
                return;

            var patchManager = Torch.Managers.GetManager<PatchManager>();
            var context = patchManager.AcquireContext();

            if (newState == TorchGameState.Loading)
            {
                _harmony.PatchAll();
            }

            if (newState != TorchGameState.Loaded)
                return;

            GameIsReady = true;

            if (Config.PveZoneEnabled)
            {
                PVE.Init(this, context);
            }

            patchManager.Commit();
        }

        public override void Update()
        {
            base.Update();

            if (!MySession.Static.IsSaveInProgress)
                MyGasTankPatch.UpdateTanks();

            ServerAliveLog.UpdateLOG();
            MyPVESafeZoneAction.UpdateBoot();
        }

        public void LoadConfig()
        {
            if (_configPersistent?.Data != null)
                _configPersistent = Persistent<DeConfig>.Load(Path.Combine(StoragePath, "DePatch.cfg"));
        }

        private void SetupConfig()
        {
            try
            {
                _configPersistent = Persistent<DeConfig>.Load(Path.Combine(StoragePath, "DePatch.cfg"));
            }
            catch (Exception ex)
            {
                Log.Warn(ex);
            }
            if (_configPersistent?.Data != null)
                return;

            Log.Info("Create Default Config, because none was found!");
            _configPersistent = new Persistent<DeConfig>(Path.Combine(StoragePath, "DePatch.cfg"), new DeConfig());
            _configPersistent.Save(null);
        }

        public UserControl GetControl()
        {
            if (Control == null)
                Control = new UserControlDepatch(this);

            return Control;
        }
        public override void Dispose()
        {
            if (_sessionManager != null)
            {
                Torch.GameStateChanged -= Torch_GameStateChanged;
            }
            _sessionManager = null;
            base.Dispose();
        }
        public class TestCommands : CommandModule
        {
            [Command("denotify", "Sends notification to all players in server.", null)]
            [Permission(MyPromoteLevel.Admin)]
            public void ShowNotification(string messageContent, int delay, string font)
            {
                MyVisualScriptLogicProvider.ShowNotificationToAll(messageContent, delay, font);
                Log.Debug("Running denotify command with contents: " + messageContent);
            }

            [Command("fancysay2", "Sends a fancy colored message in chat to all players in server.", null)]
            [Permission(MyPromoteLevel.Admin)]
            public void ShowFancyChat(string author, string font, string message)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored(message, VRageMath.Color.Coral, author, 0, font);
            }

            [Command("fancysay", "Sends a fancy colored message in chat to all players in server, with ability to change the message color. (RGBA)", null)]
            [Permission(MyPromoteLevel.Admin)]
            public void ShowFancyChat2(string author, string font, byte r, byte g, byte b, byte a, string message)
            {
                try
                {
                    ScriptedChatMsg msg = new ScriptedChatMsg();
                    msg.Author = author;
                    msg.Color = new VRageMath.Color(r, g, b, a);
                    msg.Font = font;
                    msg.Text = message;
                    MyMultiplayerBase.SendScriptedChatMessage(ref msg);
                }
                catch (Exception e)
                {
                    MyLog.Default.Error(e.Message, e.Data, e.Source);
                }   
            }
        }
    }
    [HarmonyPatch(typeof(MySession), "PerformPlatformPatchBeforeLoad", new Type[] { typeof(MyObjectBuilder_SessionSettings), typeof(MyGameModeEnum?), typeof(MyOnlineModeEnum?) })]
    class PerformPlatformPatchBeforeLoadPatch
    {
        [HarmonyPrefix]
        private static bool PerformPlatformPatchBeforeLoad()
        {
            return false; 
        }
    }
}

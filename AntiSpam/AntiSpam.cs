/*
 * Original plugin by MarioE
 */

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace AntiSpam
{
	[ApiVersion(2, 1)]
	public class AntiSpam : TerrariaPlugin
	{
		Config Config = new Config();
		DateTime[] Times = new DateTime[256];
		double[] Spams = new double[256];

		public override string Author
		{
			get { return "Zaicon"; }
		}
		public override string Description
		{
			get { return "Prevents spamming."; }
		}
		public override string Name
		{
			get { return "AntiSpam"; }
		}
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public AntiSpam(Main game)
			: base(game)
		{
			Order = 1000000;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
				ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				PlayerHooks.PlayerCommand -= OnPlayerCommand;
				GeneralHooks.ReloadEvent -= Reload;
			}
		}
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetSendData.Register(this, OnSendData);
			ServerApi.Hooks.ServerChat.Register(this, OnChat);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			PlayerHooks.PlayerCommand += OnPlayerCommand;
			GeneralHooks.ReloadEvent += Reload;
		}

		void OnChat(ServerChatEventArgs e)
		{
			if (!e.Handled)
			{
				string text = e.Text;
				if (e.Text.StartsWith(Commands.Specifier) || e.Text.StartsWith(Commands.SilentSpecifier))
					return;
				if ((DateTime.Now - Times[e.Who]).TotalSeconds > Config.Time)
				{
					Spams[e.Who] = 0.0;
					Times[e.Who] = DateTime.Now;
				}

				if (text.Trim().Length <= Config.ShortLength)
					Spams[e.Who] += Config.ShortWeight;
				else if ((double)text.Count(Char.IsUpper) / text.Length >= Config.CapsRatio)
					Spams[e.Who] += Config.CapsWeight;
				else
					Spams[e.Who] += Config.NormalWeight;

				if (Spams[e.Who] > Config.Threshold && !TShock.Players[e.Who].HasPermission("antispam.ignore"))
				{
					switch (Config.Action.ToLower())
					{
						case "ignore":
						default:
							Times[e.Who] = DateTime.Now;
							TShock.Players[e.Who].SendErrorMessage("You have been ignored for spamming.");
							e.Handled = true;
							return;
						case "kick":
							TShock.Utils.ForceKick(TShock.Players[e.Who], "Spamming", false, true);
							e.Handled = true;
							return;
					}
				}
			}
		}
		void OnInitialize(EventArgs e)
		{
			string path = Path.Combine(TShock.SavePath, "antispamconfig.json");
			if (File.Exists(path))
				Config = Config.Read(path);
			Config.Write(path);
		}
		void OnLeave(LeaveEventArgs e)
		{
			Spams[e.Who] = 0.0;
			Times[e.Who] = DateTime.Now;
		}
		void OnPlayerCommand(PlayerCommandEventArgs e)
		{
			if (!e.Handled && e.Player.RealPlayer)
			{
				switch (e.CommandName)
				{
					case "me":
					case "r":
					case "reply":
					case "tell":
					case "w":
					case "whisper":
						if ((DateTime.Now - Times[e.Player.Index]).TotalSeconds > Config.Time)
						{
							Spams[e.Player.Index] = 0.0;
							Times[e.Player.Index] = DateTime.Now;
						}

						string text = e.CommandText.Substring(e.CommandName.Length);
						if ((double)text.Where(c => Char.IsUpper(c)).Count() / text.Length >= Config.CapsRatio)
							Spams[e.Player.Index] += Config.CapsWeight;
						else if (text.Trim().Length <= Config.ShortLength)
							Spams[e.Player.Index] += Config.ShortWeight;
						else
							Spams[e.Player.Index] += Config.NormalWeight;

						if (Spams[e.Player.Index] > Config.Threshold && !TShock.Players[e.Player.Index].HasPermission("antispam.ignore"))
						{
							switch (Config.Action.ToLower())
							{
								case "ignore":
								default:
									Times[e.Player.Index] = DateTime.Now;
									TShock.Players[e.Player.Index].SendErrorMessage("You have been ignored for spamming.");
									e.Handled = true;
									return;
								case "kick":
									TShock.Utils.ForceKick(TShock.Players[e.Player.Index], "Spamming", false, true);
									e.Handled = true;
									return;
							}
						}
						return;
				}
			}
		}
		void OnSendData(SendDataEventArgs e)
		{
			if (e.MsgId == PacketTypes.ChatText && !e.Handled)
			{
				if (Config.DisableBossMessages && e.number2 == 175 && e.number3 == 75 && e.number4 == 255)
				{
					if (e.text.ToString().StartsWith("Eye of Cthulhu") || e.text.ToString().StartsWith("Eater of Worlds") ||
						e.text.ToString().StartsWith("Skeletron") || e.text.ToString().StartsWith("King Slime") ||
						e.text.ToString().StartsWith("The Destroyer") || e.text.ToString().StartsWith("The Twins") ||
						e.text.ToString().StartsWith("Skeletron Prime") || e.text.ToString().StartsWith("Wall of Flesh") ||
						e.text.ToString().StartsWith("Plantera") || e.text.ToString().StartsWith("Golem") || e.text.ToString().StartsWith("Brain of Cthulhu") ||
						e.text.ToString().StartsWith("Queen Bee") || e.text.ToString().StartsWith("Duke Fishron") ||
						e.text.ToString().StartsWith("Moon Lord"))
					{
						e.Handled = true;
					}
				}
				if (Config.DisableOrbMessages && e.number2 == 50 && e.number3 == 255 && e.number4 == 130)
				{
					if (string.Equals(e.text.ToString(), "A horrible chill goes down your spine...", StringComparison.Ordinal) ||
					    string.Equals(e.text.ToString(), "Screams echo around you...", StringComparison.Ordinal))
					{
						e.Handled = true;
					}
				}
			}
		}

		void Reload(ReloadEventArgs e)
		{
			string path = Path.Combine(TShock.SavePath, "antispamconfig.json");
			if (File.Exists(path))
				Config = Config.Read(path);
			Config.Write(path);
			e.Player.SendSuccessMessage("Reloaded antispam config.");
		}
	}
}
using System;
using System.Collections.Generic;
using System.Composition;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Interaction;

namespace ArchiSteamFarm.CustomPlugins.FirstOwned;

[Export(typeof(IPlugin))]
internal sealed class FirstOwnedPlugin : IBotCommand2 {
	public string Name => nameof(FirstOwnedPlugin);
	public Version Version => typeof(FirstOwnedPlugin).Assembly.GetName().Version ?? new Version(1, 0);

	public Task OnLoaded() {
		ASF.ArchiLogger.LogGenericInfo("FirstOwned loaded. Command: FIRSTOWNED [bots] <appID|name>");

		return Task.CompletedTask;
	}

	public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) {
		try {
			return await Handle(bot, access, args).ConfigureAwait(false);
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericException(e);

			return "FirstOwned error: " + e.GetType().Name + ": " + e.Message;
		}
	}

	private sealed class Plan {
		internal string? Error;
		internal bool NotOurs;
		internal List<Bot> Bots = new();
		internal string Game = string.Empty;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Plan MakePlan(Bot bot, EAccess access, string[] args) {
		Plan plan = new();

		if ((args.Length < 2) || !string.Equals(args[0], "FIRSTOWNED", StringComparison.OrdinalIgnoreCase)) {
			plan.NotOurs = true;

			return plan;
		}

		if (access < EAccess.Operator) {
			plan.Error = "Access denied.";

			return plan;
		}

		plan.Bots.Add(bot);
		int gameStart = 1;

		if (args.Length >= 3) {
			HashSet<Bot>? named = Bot.GetBots(args[1]);

			if ((named != null) && (named.Count > 0)) {
				if (access < EAccess.Master) {
					plan.Error = "Access denied: querying other bots needs Master access.";

					return plan;
				}

				plan.Bots.Clear();

				foreach (Bot b in named) {
					plan.Bots.Add(b);
				}

				plan.Bots.Sort(static (x, y) => string.Compare(x.BotName, y.BotName, StringComparison.OrdinalIgnoreCase));
				gameStart = 2;
			}
		}

		StringBuilder sb = new();

		for (int i = gameStart; i < args.Length; i++) {
			if (sb.Length > 0) {
				sb.Append(' ');
			}

			sb.Append(args[i]);
		}

		plan.Game = sb.ToString();

		return plan;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string Render(List<Bot> bots, uint appID, string name) {
		StringBuilder sb = new();

		foreach (Bot b in bots) {
			if (sb.Length > 0) {
				sb.Append('\n');
			}

			sb.Append(b.IsConnectedAndLoggedOn ? FirstOwnedFinder.Format(b, name, FirstOwnedFinder.Find(b, appID)) : "<" + b.BotName + "> not connected.");
		}

		return sb.ToString();
	}

	private static async Task<string?> Handle(Bot bot, EAccess access, string[] args) {
		Plan plan = MakePlan(bot, access, args);

		if (plan.NotOurs) {
			return null;
		}

		if (plan.Error != null) {
			return plan.Error;
		}

		(uint AppID, string Name)? resolved = await AppResolver.Resolve(plan.Game).ConfigureAwait(false);

		if (resolved == null) {
			return "Could not resolve that game. Use an appID (e.g. 1422450) or a store name.";
		}

		return Render(plan.Bots, resolved.Value.AppID, resolved.Value.Name);
	}
}

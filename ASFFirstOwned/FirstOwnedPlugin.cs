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
	private static string Render(List<Bot> bots, List<(uint AppID, string Name)> candidates) {
		StringBuilder sb = new();

		foreach (Bot b in bots) {
			if (sb.Length > 0) {
				sb.Append('\n');
			}

			if (!b.IsConnectedAndLoggedOn) {
				sb.Append("<" + b.BotName + "> not connected.");

				continue;
			}

			// A single candidate is an exact appID (or a one-result search): keep the simple output
			if (candidates.Count == 1) {
				sb.Append(FirstOwnedFinder.Format(b, candidates[0].Name, FirstOwnedFinder.Find(b, candidates[0].AppID)));

				continue;
			}

			// Several candidates: this was a name search, so show the ones this account actually owns
			List<(uint AppID, string Name, OwnedResult Result)> owned = new();

			foreach ((uint appID, string name) in candidates) {
				OwnedResult? result = FirstOwnedFinder.Find(b, appID);

				if (result != null) {
					owned.Add((appID, name, result));
				}
			}

			if (owned.Count == 0) {
				sb.Append("<" + b.BotName + "> none of the closest store matches are owned. Closest: ");

				for (int i = 0; (i < candidates.Count) && (i < 5); i++) {
					if (i > 0) {
						sb.Append(", ");
					}

					sb.Append(candidates[i].Name + " (" + candidates[i].AppID + ")");
				}

				continue;
			}

			sb.Append(FirstOwnedFinder.Format(b, owned[0].Name, owned[0].Result));

			for (int i = 1; (i < owned.Count) && (i < 5); i++) {
				sb.Append('\n');
				sb.Append("  also matches: " + FirstOwnedFinder.FormatShort(owned[i].Name, owned[i].AppID, owned[i].Result));
			}
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

		List<(uint AppID, string Name)> candidates = await AppResolver.Resolve(plan.Game).ConfigureAwait(false);

		if (candidates.Count == 0) {
			return "No store match for that. Use an appID (e.g. 1422450) or part of the game's name.";
		}

		return Render(plan.Bots, candidates);
	}
}

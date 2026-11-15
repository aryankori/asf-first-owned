using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Data;
using ArchiSteamFarm.Storage;

namespace ArchiSteamFarm.CustomPlugins.FirstOwned;

internal sealed record OwnedResult(uint AppID, LicenseData First, int PackageCount);

internal static class FirstOwnedFinder {
	// Steam uses the unix epoch as a placeholder for licenses with no real creation time
	private static readonly DateTime Placeholder = DateTime.UnixEpoch.AddDays(1);

	internal static OwnedResult? Find(Bot bot, uint appID) {
		ArgumentNullException.ThrowIfNull(bot);

		if (ASF.GlobalDatabase == null) {
			return null;
		}

		IReadOnlyDictionary<uint, PackageData> packagesData = ASF.GlobalDatabase.PackagesDataReadOnly;

		List<LicenseData> matches = [];

		foreach ((uint packageID, LicenseData license) in bot.OwnedPackages) {
			if (packagesData.TryGetValue(packageID, out PackageData? data) && (data.AppIDs?.Contains(appID) == true)) {
				matches.Add(license);
			}
		}

		if (matches.Count == 0) {
			return null;
		}

		// Prefer licenses with a real timestamp, fall back to whatever exists
		LicenseData first = matches.Where(static l => l.TimeCreated > Placeholder).OrderBy(static l => l.TimeCreated).FirstOrDefault() ?? matches.OrderBy(static l => l.TimeCreated).First();

		return new OwnedResult(appID, first, matches.Count);
	}

	internal static string FormatShort(string name, uint appID, OwnedResult result) {
		DateTime utc = DateTime.SpecifyKind(result.First.TimeCreated, DateTimeKind.Utc);

		return name + " (" + appID.ToString(CultureInfo.InvariantCulture) + "): first owned " + utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC via " + result.First.PaymentMethod;
	}

	internal static string Format(Bot bot, string gameLabel, OwnedResult? result) {
		if (result == null) {
			return $"<{bot.BotName}> {gameLabel}: not owned, or package data not loaded yet (try again after ASF has synced).";
		}

		DateTime utc = DateTime.SpecifyKind(result.First.TimeCreated, DateTimeKind.Utc);
		string when = utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

		string note = result.First.TimeCreated <= Placeholder ? " (Steam reports no real timestamp for this license)" : string.Empty;
		string extra = result.PackageCount > 1 ? $", {result.PackageCount.ToString(CultureInfo.InvariantCulture)} packages grant it, showing the earliest" : string.Empty;

		return $"<{bot.BotName}> {gameLabel}: first owned {when} via {result.First.PaymentMethod} (package {result.First.PackageID.ToString(CultureInfo.InvariantCulture)}{extra}){note}";
	}
}

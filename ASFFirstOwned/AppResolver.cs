using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Web;
using ArchiSteamFarm.Web.Responses;

namespace ArchiSteamFarm.CustomPlugins.FirstOwned;

internal static class AppResolver {
	// Accepts "1422450", "app/1422450" or a game name. Returns (appID, display name) or null.
	// Note: ASF ships as a trimmed single-file build, so only well-trodden framework APIs and ASF's own WebBrowser are used here.
	internal static async Task<(uint AppID, string Name)?> Resolve(string query) {
		if (string.IsNullOrWhiteSpace(query)) {
			return null;
		}

		query = query.Trim();

		if (query.StartsWith("app/", StringComparison.OrdinalIgnoreCase)) {
			query = query.Substring(4);
		}

		if (uint.TryParse(query, out uint appID) && (appID > 0)) {
			return (appID, await GetName(appID).ConfigureAwait(false));
		}

		if (ASF.WebBrowser == null) {
			return null;
		}

		Uri url = new("https://store.steampowered.com/api/storesearch/?cc=us&l=english&term=" + Uri.EscapeDataString(query));

		ObjectResponse<SearchResponse>? response = await ASF.WebBrowser.UrlGetToJsonObject<SearchResponse>(url).ConfigureAwait(false);

		if (response?.Content?.Items is not { Count: > 0 } items) {
			return null;
		}

		return (items[0].ID, items[0].Name);
	}

	// Works for games, DLC, demos and other apps. Falls back to "app <id>" when the store has no public listing (delisted, region locked, etc).
	private static async Task<string> GetName(uint appID) {
		string fallback = "app " + appID;

		if (ASF.WebBrowser == null) {
			return fallback;
		}

		try {
			Uri url = new("https://store.steampowered.com/api/appdetails?filters=basic&l=english&appids=" + appID);

			ObjectResponse<Dictionary<string, AppDetailsEntry>>? response = await ASF.WebBrowser.UrlGetToJsonObject<Dictionary<string, AppDetailsEntry>>(url).ConfigureAwait(false);

			if ((response?.Content != null) && response.Content.TryGetValue(appID.ToString(), out AppDetailsEntry? entry) && entry.Success && !string.IsNullOrEmpty(entry.Data?.Name)) {
				return entry.Data.Name;
			}
		} catch (Exception) {
			// The name is cosmetic, never fail the command because of it
		}

		return fallback;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the JSON deserializer")]
	private sealed class AppDetailsEntry {
		[JsonInclude]
		[JsonPropertyName("success")]
		public bool Success { get; private init; }

		[JsonInclude]
		[JsonPropertyName("data")]
		public AppDetailsData? Data { get; private init; }
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the JSON deserializer")]
	private sealed class AppDetailsData {
		[JsonInclude]
		[JsonPropertyName("name")]
		public string Name { get; private init; } = string.Empty;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the JSON deserializer")]
	private sealed class SearchResponse {
		[JsonInclude]
		[JsonPropertyName("items")]
		public List<SearchItem>? Items { get; private init; }
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the JSON deserializer")]
	private sealed class SearchItem {
		[JsonInclude]
		[JsonPropertyName("id")]
		public uint ID { get; private init; }

		[JsonInclude]
		[JsonPropertyName("name")]
		public string Name { get; private init; } = string.Empty;
	}
}

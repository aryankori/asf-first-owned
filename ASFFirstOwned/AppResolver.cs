using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Web.Responses;

namespace ArchiSteamFarm.CustomPlugins.FirstOwned;

internal static class AppResolver {
	private const int MaxCandidates = 10;

	// Accepts "1422450", "app/1422450", a full name or a partial name.
	// An appID yields exactly one candidate. A name yields the store search results (best match first), so the caller can pick the ones the account owns.
	// Note: ASF ships as a trimmed single-file build, so only well-trodden framework APIs and ASF's own WebBrowser are used here.
	internal static async Task<List<(uint AppID, string Name)>> Resolve(string query) {
		List<(uint AppID, string Name)> result = new();

		if (string.IsNullOrWhiteSpace(query)) {
			return result;
		}

		query = query.Trim();

		if (query.StartsWith("app/", StringComparison.OrdinalIgnoreCase)) {
			query = query.Substring(4);
		}

		if (uint.TryParse(query, out uint appID) && (appID > 0)) {
			result.Add((appID, await GetName(appID).ConfigureAwait(false)));

			return result;
		}

		if (ASF.WebBrowser == null) {
			return result;
		}

		try {
			Uri url = new("https://store.steampowered.com/api/storesearch/?cc=us&l=english&term=" + Uri.EscapeDataString(query));

			ObjectResponse<SearchResponse>? response = await ASF.WebBrowser.UrlGetToJsonObject<SearchResponse>(url).ConfigureAwait(false);

			if (response?.Content?.Items == null) {
				return result;
			}

			foreach (SearchItem item in response.Content.Items) {
				if (item.ID == 0) {
					continue;
				}

				result.Add((item.ID, item.Name));

				if (result.Count >= MaxCandidates) {
					break;
				}
			}
		} catch (Exception) {
			// Searching is best effort, an empty list is reported to the user as "no match"
		}

		return result;
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

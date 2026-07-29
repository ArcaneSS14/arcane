// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.Administration.Managers;

/// <summary>
///     This manager sends a webhook notification whenever a player with an active
///     watchlist joins the server.
/// </summary>
public interface IWatchlistWebhookManager
{
    void Initialize();
    void Update();
}
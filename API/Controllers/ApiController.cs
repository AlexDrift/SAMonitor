﻿using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SAMonitor.Data;
using SAMonitor.Utils;
using System.Security.Cryptography.X509Certificates;

namespace SAMonitor.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly ILogger<ApiController> _logger;

    public ApiController(ILogger<ApiController> logger)
    {
        _logger = logger;
    }

    [HttpGet("GetServerByIP")]
    public Server? GetServerByIP(string ip_addr)
    {
        ServerManager.ApiHits++;
        Server? result = ServerManager.ServerByIP(ip_addr);

        return result;
    }

    [HttpGet("GetAllServers")]
    public IEnumerable<Server> GetAllServers()
    {
        ServerManager.ApiHits++;

        return ServerManager.GetServers();
    }

    [HttpGet("GetFilteredServers")]
    public List<Server> GetFilteredServers(int show_empty = 0, string order = "none", string name = "unspecified", string gamemode = "unspecified", int hide_roleplay = 0, int paging_size = 0, int page = 0, string version = "any", string language = "any", int require_sampcac = 0, int show_passworded = 0)
    {
        ServerManager.ApiHits++;

        ServerFilterer filterServers = new(
                showEmpty: show_empty != 0,
                showPassworded: show_passworded != 0,
                hideRoleplay: hide_roleplay != 0,
                requireSampCAC: require_sampcac != 0,
                order: order,
                name: name.ToLower(),
                gamemode: gamemode.ToLower(),
                language: language.ToLower(),
                version: version.ToLower()
            );

        List<Server> orderedServers = filterServers.GetFilteredServers();

        // If we're paging, return a "page".
        if (paging_size > 0)
        {
            try
            {
                return orderedServers.Skip(paging_size * page).Take(paging_size).ToList();
            }
            catch
            {
                // nothing, just return the full list. this just protects from malicious pagingSize or page values.
            }
        }

        return orderedServers;
    }

    [HttpGet("GetServerPlayers")]
    public List<Player> GetServerPlayers(string ip_addr)
    {
        ServerManager.ApiHits++;

        var result = ServerManager.ServerByIP(ip_addr);

        if (result is null) return new List<Player>();

        return result.GetPlayers();
    }

    [HttpGet("GetGlobalStats")]
    public GlobalStats GetGlobalStats()
    {
        return
            new GlobalStats(
                serversTracked: ServerManager.ServerCount(1),
                serversOnline: ServerManager.ServerCount(0),
                playersOnline: ServerManager.TotalPlayers()
            );
    }

    [HttpGet("GetLanguageStats")]
    public LanguageStats GetLanguageStats()
    {
        return ServerManager.LanguageAnalytics();
    }

    [HttpGet("GetGamemodeStats")]
    public GamemodeStats GetGamemodeStats()
    {
        return ServerManager.GamemodeAnalytics();
    }

    [HttpGet("GetMasterlist")]
    public string GetMasterlist(string version = "any")
    {
        ServerManager.ApiHits++;

        return ServerManager.GetMasterlist(version);
    }

    private long lastAddReq = 0;

    [HttpGet("AddServer")]
    public async Task<string> AddServer(string ip_addr)
    {
        if (lastAddReq >= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return "Please try again in a few seconds.";
        }

        lastAddReq = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3;

        ServerManager.ApiHits++;

        ip_addr = ip_addr.Trim();
        string validIP = Helpers.ValidateIPv4(ip_addr);
        if (validIP != "invalid")
            return (await ServerManager.AddServer(validIP));
        else
            return "Entered IP address or hostname is invalid or failing to resolve.";
    }

    [HttpGet("GetGlobalMetrics")]
    public async Task<List<GlobalMetrics>> GetGlobalMetrics(int hours = 6)
    {
        DateTime RequestTime = DateTime.Now - TimeSpan.FromHours(hours);

        var conn = new MySqlConnection(MySQL.ConnectionString);
        var sql = @"SELECT players, servers, api_hits, time FROM metrics_global WHERE time > @RequestTime ORDER BY time DESC";

        return (await conn.QueryAsync<GlobalMetrics>(sql, new { RequestTime })).ToList();
    }

    [HttpGet("GetServerMetrics")]
    public async Task<dynamic> GetServerMetrics(string ip_addr = "none", int hours = 6, int include_misses = 0)
    {
        ServerManager.ApiHits++;

        DateTime RequestTime = DateTime.Now - TimeSpan.FromHours(hours);

        int Id = ServerManager.GetServerIDFromIP(ip_addr);

        var conn = new MySqlConnection(MySQL.ConnectionString);

        string sql;

        // "Misses" are times where the server was down at the time of being queried. This is recorded as having -1 players online.
        // This data might be misleading or undesired, as such, we don't include it unless explicitly requested.
        if (include_misses > 0)
        {
            sql = @"SELECT players, time FROM metrics_server WHERE time > @RequestTime AND server_id = @Id ORDER BY time DESC";
        }
        else
        {
            sql = @"SELECT players, time FROM metrics_server WHERE time > @RequestTime AND server_id = @Id AND players >= 0 ORDER BY time DESC";
        }

        return (await conn.QueryAsync<ServerMetrics>(sql, new { RequestTime, Id })).ToList();
    }






    // TO BE DEPRECATED

    [HttpGet("GetTotalPlayers")]
    public int GetTotalPlayers()
    {
        return ServerManager.TotalPlayers();
    }

    [HttpGet("GetAmountServers")]
    public int GetAmountServers(int include_dead = 0)
    {
        return ServerManager.ServerCount(include_dead);
    }
}
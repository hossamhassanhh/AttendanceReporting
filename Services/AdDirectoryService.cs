using System.DirectoryServices.Protocols;
using System.Net;
using AttendanceApp.Data;
using AttendanceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApp.Services;

public record AdUserInfo(string Username, string DisplayName, string? Department, string? JobTitle, string? WorkLocation);

public record AdSyncResult(int Added, int Updated, int Skipped, DateTime CompletedAt);

public class AdDirectoryService
{
    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<AdDirectoryService> _logger;

    public AdDirectoryService(
        IConfiguration configuration,
        IDbContextFactory<AppDbContext> factory,
        ILogger<AdDirectoryService> logger)
    {
        _configuration = configuration;
        _factory = factory;
        _logger = logger;
    }

    public bool Enabled =>
        _configuration.GetValue("ActiveDirectory:Enabled", true)
        && !string.IsNullOrWhiteSpace(BindPassword);

    private string Server => _configuration["ActiveDirectory:Server"] ?? "10.51.0.22";
    private int Port => _configuration.GetValue("ActiveDirectory:Port", 389);
    private string Domain => _configuration["ActiveDirectory:Domain"] ?? "PMS";
    private string BindUser => _configuration["ActiveDirectory:BindUser"] ?? "4690";
    private string? BindPassword => _configuration["ActiveDirectory:BindPassword"];
    private string SearchBase => _configuration["ActiveDirectory:SearchBase"] ?? "DC=PMS,DC=LOCAL";

    private LdapConnection CreateConnection(string username, string password)
    {
        var connection = new LdapConnection(
            new LdapDirectoryIdentifier(Server, Port))
        {
            AuthType = AuthType.Negotiate,
            Credential = new NetworkCredential(username, password, Domain),
            Timeout = TimeSpan.FromSeconds(10)
        };
        connection.SessionOptions.ProtocolVersion = 3;
        return connection;
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        try
        {
            using var connection = CreateConnection(BindUser, BindPassword!);
            await Task.Run(() => connection.Bind());

            var filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeFilterValue(username)}))";
            var request = new SearchRequest(
                SearchBase,
                filter,
                SearchScope.Subtree,
                "sAMAccountName");
            var response = (SearchResponse)connection.SendRequest(request);
            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
                return false;

            using var userConnection = CreateConnection(username, password);
            await Task.Run(() => userConnection.Bind());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AD credential validation failed for {Username}", username);
            return false;
        }
    }

    public async Task<AdUserInfo?> GetUserInfoAsync(string username)
    {
        if (!Enabled)
            return null;

        try
        {
            using var connection = CreateConnection(BindUser, BindPassword!);
            await Task.Run(() => connection.Bind());

            var filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeFilterValue(username)}))";
            var request = new SearchRequest(
                SearchBase,
                filter,
                SearchScope.Subtree,
                "sAMAccountName", "displayName", "department", "title", "description");
            var response = (SearchResponse)connection.SendRequest(request);
            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
                return null;

            var entry = response.Entries[0];
            var sam = GetAttributeValue(entry, "sAMAccountName");
            if (string.IsNullOrWhiteSpace(sam))
                return null;

            return new AdUserInfo(
                sam,
                GetAttributeValue(entry, "displayName") ?? sam,
                GetAttributeValue(entry, "department"),
                GetAttributeValue(entry, "title"),
                GetAttributeValue(entry, "description"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AD user lookup failed for {Username}", username);
            return null;
        }
    }

    public async Task<AdSyncResult> SyncUsersAsync()
    {
        if (!Enabled)
            return new AdSyncResult(0, 0, 0, DateTime.Now);

        using var connection = CreateConnection(BindUser, BindPassword!);
        await Task.Run(() => connection.Bind());

        var request = new SearchRequest(
            SearchBase,
            "(&(objectCategory=person)(objectClass=user)(sAMAccountName=*))",
            SearchScope.Subtree,
            "sAMAccountName", "displayName", "department", "title", "description");
        var response = (SearchResponse)connection.SendRequest(request);
        if (response.ResultCode != ResultCode.Success)
            throw new InvalidOperationException($"AD search failed with result code {response.ResultCode}");

        var candidates = new List<AdUserInfo>();
        foreach (SearchResultEntry entry in response.Entries)
        {
            var sam = GetAttributeValue(entry, "sAMAccountName");
            if (string.IsNullOrWhiteSpace(sam) || !long.TryParse(sam, out _))
                continue;

            candidates.Add(new AdUserInfo(
                sam,
                GetAttributeValue(entry, "displayName") ?? sam,
                GetAttributeValue(entry, "department"),
                GetAttributeValue(entry, "title"),
                GetAttributeValue(entry, "description")));
        }

        var added = 0;
        var updated = 0;
        var skipped = 0;

        using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Employees
            .Where(e => e.IsActive)
            .Select(e => e.FinancialNo)
            .ToListAsync();
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var info in candidates)
        {
            var financialNo = info.Username;
            var employee = await db.Employees.FindAsync(financialNo);
            if (employee == null)
            {
                db.Employees.Add(new Employee
                {
                    FinancialNo = financialNo,
                    Name = info.DisplayName,
                    Department = info.Department,
                    JobTitle = info.JobTitle,
                    WorkLocation = info.WorkLocation,
                    IsActive = true
                });
                added++;
            }
            else
            {
                // Preserve Arabic names and sheet data for existing employees.
                // The work-schedule sheet (PendingWorkScheduleImport) is the source of truth for Name/Department/Level.
                // AD sync should only fill missing data or update placeholders ("Employee {FinancialNo}" / "موظف {FinancialNo}"), not overwrite Arabic names.
                var isPlaceholder = employee.Name.StartsWith("Employee ", StringComparison.OrdinalIgnoreCase)
                    || employee.Name.StartsWith("موظف ", StringComparison.Ordinal);
                var hasChanges = false;

                if ((string.IsNullOrWhiteSpace(employee.Name) || isPlaceholder) && !string.IsNullOrWhiteSpace(info.DisplayName))
                {
                    employee.Name = info.DisplayName;
                    hasChanges = true;
                }
                if (string.IsNullOrWhiteSpace(employee.Department) && !string.IsNullOrWhiteSpace(info.Department))
                {
                    employee.Department = info.Department;
                    hasChanges = true;
                }
                if (string.IsNullOrWhiteSpace(employee.JobTitle) && !string.IsNullOrWhiteSpace(info.JobTitle))
                {
                    employee.JobTitle = info.JobTitle;
                    hasChanges = true;
                }
                if (string.IsNullOrWhiteSpace(employee.WorkLocation) && !string.IsNullOrWhiteSpace(info.WorkLocation))
                {
                    employee.WorkLocation = info.WorkLocation;
                    hasChanges = true;
                }

                if (hasChanges)
                    updated++;
                else
                    skipped++;
            }
        }

        await db.SaveChangesAsync();

        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.Key == "AdSync");
        if (state == null)
        {
            state = new SyncState { Key = "AdSync" };
            db.SyncStates.Add(state);
        }
        state.LastProcessedTransactionId = added + updated;
        state.LastSyncTime = DateTime.Now;
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "AD sync completed: {Added} added, {Updated} updated, {Skipped} unchanged, {Total} AD users found",
            added, updated, skipped, candidates.Count);
        return new AdSyncResult(added, updated, skipped, DateTime.Now);
    }

    public async Task<object?> GetSyncStatusAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        var state = await db.SyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "AdSync");
        if (state == null)
            return null;
        return new
        {
            state.LastSyncTime,
            LastChangeCount = state.LastProcessedTransactionId
        };
    }

    private static string EscapeFilterValue(string value) =>
        value.Replace("\\", "\\5c").Replace("*", "\\2a")
            .Replace("(", "\\28").Replace(")", "\\29")
            .Replace("\0", "\\00");

    private static string? GetAttributeValue(SearchResultEntry entry, string name)
    {
        if (!entry.Attributes.Contains(name))
            return null;
        var values = entry.Attributes[name].GetValues(typeof(string));
        if (values == null || values.Length == 0)
            return null;
        var text = (values[0] as string)?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
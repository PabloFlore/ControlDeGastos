using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IAccountLifecycleService
{
    Task<CloudConnectionResult> ConnectCloudAsync(string email, string password);
    Task<AccountResult> DisconnectCloudAsync();
    Task<CloudConnectionResult> ReauthenticateCloudAsync(string email, string password);
    Task<AccountResult> ToggleGamificationAsync(bool activate);
    Task<AccountResult> CreateHouseholdAsync();
    Task<AccountResult> JoinHouseholdAsync(string codigo, string email);
    Task<AccountResult> LeaveHouseholdAsync(string hogarId, string email);
    Task<AccountResult> LogoutAndClearAsync();
    Task<AccountResult> LogoutCloudOnlyAsync();
    Task LimpiarDatosLocalesAsync();
}

public class CloudConnectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AccountResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

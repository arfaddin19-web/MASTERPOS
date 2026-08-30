namespace MasterPOS.Application.Setup;

public interface ISetupService
{
    Task<SetupStatusResponse> GetStatusAsync(CancellationToken ct = default);
    Task<SetupCompanyResponse> CompleteSetupAsync(SetupCompanyRequest request, CancellationToken ct = default);
}

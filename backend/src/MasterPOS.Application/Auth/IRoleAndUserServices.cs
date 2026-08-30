namespace MasterPOS.Application.Auth;

public interface IRoleService
{
    Task<RoleDto> CreateAsync(UpsertRoleRequest request, CancellationToken ct = default);
    Task<RoleDto> UpdateAsync(Guid id, UpsertRoleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<RoleDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default);
}

public interface IUserService
{
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<UserDto> SetActiveAsync(Guid id, SetUserActiveRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default);
    Task<UserDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default);
}

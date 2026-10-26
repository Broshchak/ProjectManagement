namespace Application.Common.Interfaces
{
    public enum PasswordVerifyResult
    {
        Failed,
        Success,
        SuccessRehashNeeded
    }

    public interface IPasswordHasher
    {
        Task<string> HashAsync(string password, CancellationToken ct = default);

        Task<PasswordVerifyResult> VerifyAsync(string passwordHash, string password, CancellationToken ct = default);

        Task VerifyDummyAsync(string password, CancellationToken ct = default);
    }
}

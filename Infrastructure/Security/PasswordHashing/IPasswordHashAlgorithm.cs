namespace Infrastructure.Security.PasswordHashing
{
    public interface IPasswordHashAlgorithm
    {
        string Id { get; }

        bool IsMemoryHard { get; }

        string Hash(string password);

        bool IsSupported(PhcHash hash);

        bool Verify(PhcHash hash, string password);

        bool NeedsRehash(PhcHash hash);
    }
}

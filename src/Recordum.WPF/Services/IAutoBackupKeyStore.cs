namespace Recordum.WPF.Services;

public interface IAutoBackupKeyStore
{
    bool HasKey();
    void WriteKey(string passphrase);
    void DeleteKey();
}

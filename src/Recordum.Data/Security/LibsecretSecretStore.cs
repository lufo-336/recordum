using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>Secret Service via libsecret (P/Invoke, nessuna dipendenza). Schema <c>org.recordum.Secret</c> con gli
/// attributi <c>kind</c> e <c>data_dir</c> (percorso normalizzato): due installazioni non si vedono. Forma delle
/// chiamate ripresa da Git Credential Manager (MIT). Il valore non finisce mai in un messaggio né in un log.</summary>
[SupportedOSPlatform("linux")]
public sealed partial class LibsecretSecretStore : ISecretStore
{
    private const string Lib = "libsecret-1.so.0";
    private const string GLib = "libglib-2.0.so.0";
    private const string GObject = "libgobject-2.0.so.0";
    private const int SecretSearchAll = 1 << 1;   // SECRET_SEARCH_ALL: anche le voci bloccate, senza sbloccare ne' chiedere
    private const string SchemaName = "org.recordum.Secret";

    private readonly string _dataDir;
    private readonly string _library;
    private readonly Lazy<bool> _available;

    public LibsecretSecretStore(string dataDirectory) : this(dataDirectory, Lib) { }

    internal LibsecretSecretStore(string dataDirectory, string libraryName)
    {
        _dataDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        _library = libraryName;
        _available = new Lazy<bool>(Probe);
    }

    public bool IsAvailable => _available.Value;

    public string? TryRead(SecretKind kind)
    {
        Require();
        return Call(schema =>
        {
            var p = secret_password_lookup_sync(schema, IntPtr.Zero, out var err, "kind", Name(kind), "data_dir", _dataDir, IntPtr.Zero);
            ThrowIf(err);
            if (p == IntPtr.Zero)
            {
                // Un prompt di sblocco annullato torna "niente" senza errore, come un segreto che non c'e'. Se la voce
                // esiste ed e' bloccata, il portachiavi non e' raggiungibile: non e' una passphrase mancante (F2c-8),
                // e lo scheduler non deve spegnere il backup automatico.
                if (ExistsLocked(schema, kind)) throw new SecretStoreUnavailableException("The system keyring is locked.");
                return null;
            }
            try { return Marshal.PtrToStringUTF8(p); }
            finally { secret_password_free(p); }
        });
    }

    public void Write(SecretKind kind, string value)
    {
        Require();
        // Etichette visibili in Seahorse: inglese e non localizzate (R7, principio English-first).
        var label = kind == SecretKind.DbKey
            ? $"Recordum database key ({_dataDir})"
            : $"Recordum auto-backup passphrase ({_dataDir})";
        Call<object?>(schema =>
        {
            var stored = secret_password_store_sync(schema, "default", label, value, IntPtr.Zero, out var err, "kind", Name(kind), "data_dir", _dataDir, IntPtr.Zero);
            ThrowIf(err);
            // FALSE senza GError (es. prompt di sblocco annullato): il segreto non e' stato scritto, e dirlo e' meglio che crederlo.
            if (!stored) throw new SecretStoreUnavailableException("The system keyring did not store the secret.");
            return null;
        });
    }

    public void Delete(SecretKind kind)
    {
        Require();
        Call<object?>(schema =>
        {
            secret_password_clear_sync(schema, IntPtr.Zero, out var err, "kind", Name(kind), "data_dir", _dataDir, IntPtr.Zero);
            ThrowIf(err);
            return null;
        });
    }

    /// <summary>La voce c'e' (bloccata o no) secondo una ricerca che non sblocca. Su un libsecret senza
    /// <c>secret_password_search_sync</c> (prima della 0.19) si torna al comportamento di prima: assente.</summary>
    private bool ExistsLocked(IntPtr schema, SecretKind kind)
    {
        IntPtr list;
        try { list = secret_password_search_sync(schema, SecretSearchAll, IntPtr.Zero, out var err, "kind", Name(kind), "data_dir", _dataDir, IntPtr.Zero); ThrowIf(err); }
        catch (EntryPointNotFoundException) { return false; }
        if (list == IntPtr.Zero) return false;
        // GList { data, next, prev }: ogni data e' un SecretRetrievable (GObject) da rilasciare.
        for (var node = list; node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size))
            g_object_unref(Marshal.ReadIntPtr(node));
        g_list_free(list);
        return true;
    }

    private static string Name(SecretKind kind) => kind == SecretKind.DbKey ? "db-key" : "auto-backup";

    private void Require()
    {
        if (!IsAvailable) throw new SecretStoreUnavailableException("The system keyring is not available.");
    }

    // La libreria si carica una volta; poi una lettura di prova dice se il servizio risponde (assenza = null, non errore).
    private bool Probe()
    {
        if (!NativeLibrary.TryLoad(_library, out var h)) return false;
        NativeLibrary.Free(h);
        try
        {
            Call(schema =>
            {
                var p = secret_password_lookup_sync(schema, IntPtr.Zero, out var err, "kind", "probe", "data_dir", _dataDir, IntPtr.Zero);
                ThrowIf(err);
                if (p != IntPtr.Zero) secret_password_free(p);
                return (object?)null;
            });
            return true;
        }
        catch (SecretStoreUnavailableException) { return false; }
    }

    private static T Call<T>(Func<IntPtr, T> body)
    {
        // SecretSchema: name*, flags, attributes[32] {name*, type}, + riservati. 640 byte azzerati coprono la struttura.
        var schema = Marshal.AllocHGlobal(640);
        var name = Marshal.StringToCoTaskMemUTF8(SchemaName);
        var kind = Marshal.StringToCoTaskMemUTF8("kind");
        var dir = Marshal.StringToCoTaskMemUTF8("data_dir");
        try
        {
            for (var i = 0; i < 640; i += 8) Marshal.WriteInt64(schema, i, 0);
            Marshal.WriteIntPtr(schema, 0, name);
            Marshal.WriteInt32(schema, 8, 0); // SECRET_SCHEMA_NONE
            Marshal.WriteIntPtr(schema, 16, kind);
            Marshal.WriteInt32(schema, 24, 0); // SECRET_SCHEMA_ATTRIBUTE_STRING
            Marshal.WriteIntPtr(schema, 32, dir);
            Marshal.WriteInt32(schema, 40, 0);
            return body(schema);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            throw new SecretStoreUnavailableException("The system keyring library could not be loaded.", e);
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
            Marshal.FreeCoTaskMem(kind);
            Marshal.FreeCoTaskMem(dir);
            Marshal.FreeHGlobal(schema);
        }
    }

    private static void ThrowIf(IntPtr gerror)
    {
        if (gerror == IntPtr.Zero) return;
        string? msg;
        try { msg = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(gerror, 8)); } // GError { domain, code, message* }
        finally { g_error_free(gerror); }
        throw new SecretStoreUnavailableException("The system keyring reported an error: " + msg);
    }

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr secret_password_lookup_sync(IntPtr schema, IntPtr cancellable, out IntPtr error,
        string a1, string v1, string a2, string v2, IntPtr end);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool secret_password_store_sync(IntPtr schema, string collection, string label, string password,
        IntPtr cancellable, out IntPtr error, string a1, string v1, string a2, string v2, IntPtr end);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool secret_password_clear_sync(IntPtr schema, IntPtr cancellable, out IntPtr error,
        string a1, string v1, string a2, string v2, IntPtr end);

    [LibraryImport(Lib)]
    private static partial void secret_password_free(IntPtr password);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr secret_password_search_sync(IntPtr schema, int flags, IntPtr cancellable, out IntPtr error,
        string a1, string v1, string a2, string v2, IntPtr end);

    [LibraryImport(GLib)]
    private static partial void g_list_free(IntPtr list);

    [LibraryImport(GObject)]
    private static partial void g_object_unref(IntPtr obj);

    [LibraryImport(GLib)]
    private static partial void g_error_free(IntPtr error);
}

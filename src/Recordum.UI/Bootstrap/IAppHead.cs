using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Startup;

namespace Recordum.UI.Bootstrap;

/// <summary>La testa: cosa cambia fra computer e telefono. Due implementazioni: DesktopHead (Recordum.Desktop), AndroidHead (più avanti).</summary>
public interface IAppHead
{
    /// <summary>Argomenti di avvio del processo (computer: argv; telefono: vuoto).</summary>
    IReadOnlyList<string> Args { get; }

    /// <summary>Risolve le opzioni di avvio (computer: <c>RecordumOptions.Resolve</c>; telefono: cartella dati privata).</summary>
    RecordumOptions ResolveOptions();

    /// <summary>Costruisce il deposito segreti della piattaforma (computer: libsecret su Linux, Null altrove; telefono: Keystore).</summary>
    ISecretStore CreateSecretStore(RecordumOptions options);

    /// <summary>Motivo esposto se il portachiavi non è disponibile (computer: NoKeyring su Linux, NotSupported altrove; telefono: proprio del dispositivo).</summary>
    RememberUnavailableReason MissingKeyringReason { get; }

    /// <summary>Tempo massimo d'attesa per lo sblocco con chiave ricordata (computer: 10 s; telefono: infinito).</summary>
    TimeSpan StoredKeyCap { get; }

    /// <summary>Vero se la chiave del database e del backup automatico usa DPAPI di Windows (computer: su Windows; telefono: falso).</summary>
    bool UsesWindowsDpapi { get; }

    /// <summary>Densità predefinita delle righe della Home (computer: Larga; telefono: Compatta).</summary>
    HomeRows DefaultHomeRows { get; }

    /// <summary>Registra i servizi specifici della testa nel contenitore DI (computer: shell desktop; telefono: shell telefono + servizi Android).</summary>
    void AddShellServices(IServiceCollection services);

    /// <summary>Vero se questa testa avvia lo scheduler di Unisci (computer: vero; telefono: falso finché non c'è lo store SAF).</summary>
    bool RunsSync { get; }

    /// <summary>Tempo trascorso dall'avvio del processo, per le misure di startup (computer e telefono).</summary>
    TimeSpan ElapsedSinceProcessStart();

    /// <summary>Scrive una riga diagnostica nel log della testa (computer: file in logs/; telefono: log + logcat).</summary>
    void Report(string line);
}

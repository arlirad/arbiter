using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Arbiter.Network;
using Arbiter.Services;

namespace Arbiter;

internal class Server(
    Acceptor acceptor,
    SessionFactory sessionFactory
)
{
    public string Version { get => "Arbiter 2.00"; }
    public string ConfigRoot { get; private set; } = "/etc/arbiter/";
    public string ConfigExtension { get; private set; } = "";

    public readonly static Cache Cache = new();
    public readonly static Acceptor Listener = new();
    public readonly static Receiver Receiver = new();
    public readonly static Handler Handler = new();
    public readonly static Random Random = new();

    /*public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ConfigRoot = "./cfg/";
            ConfigExtension = ".cfg";
        }

        if (args.Contains("--local-config"))
            ConfigRoot = "./cfg/";

        SetPorts();

        Receiver.Requested += Receiver_Requested;
        Listener.OnConnection += Listener_OnConnection;

        Listener.Start();

        UpdateCerts();

        while (true)
            Thread.Sleep(-1);
    }*/

    public async Task Run()
    {
        acceptor.Bind(IPAddress.IPv6Any);
        acceptor.Bind(8080);
        acceptor.Start();

        while (true)
        {
            var socket = await acceptor.Accept();
            var session = sessionFactory.Create(socket);

            Receive(session);
        }
    }

    private void Receive(Session session)
    {
        _ = session.Receive()
            .ContinueWith((result) => ReceiveComplete(session, result))
            .ConfigureAwait(false);
    }

    private async Task ReceiveComplete(Session session, Task<SessionReceiveResult> task)
    {
        var result = await task;

        if (result.IsClosed || result.IsBad)
            return;

        Receive(session);
    }

    private static void SetPorts()
    {
        foreach (var site in Handler.Sites)
        {
            foreach (var binding in site.Value.Bindings)
            {
                int port = binding.Port;
                Listener.Bind(port);
            }
        }
    }

    private static void UpdateCerts()
    {
        if (!File.Exists("./acme.sh"))
        {
            Console.WriteLine("./acme.sh not found, unable to update certificates");
            return;
        }

        Dictionary<string, Site> certifiable = new Dictionary<string, Site>();

        foreach (var sitePair in Handler.Sites)
        {
            foreach (var binding in sitePair.Value.Bindings)
                if (binding.Scheme == "https" && binding.Port == 443)
                    certifiable[binding.Host] = sitePair.Value;
        }

        foreach (var pair in certifiable)
        {
            if (pair.Key == "localhost")
                continue;

            var host = pair.Key;
            var site = pair.Value;

            Console.WriteLine($"Updating {host}");

            Directory.CreateDirectory("./pfx");
            Process.Start("./acme.sh", $"--issue -d {host} -w {site.Path} --home acme/").WaitForExit();
            Process.Start("openssl", $"pkcs12 -export -out pfx/{host}.pfx -inkey acme/{host}_ecc/{host}.key -in acme/{host}_ecc/fullchain.cer -passout pass:").WaitForExit();
        }
    }
}
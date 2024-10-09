using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Arbiter;

public static class Server
{
    public static string Version { get => "Arbiter 2.00"; }

    public static Cache Cache = new Cache();
    public static Listener Listener = new Listener();
    public static Receiver Receiver = new Receiver();
    public static Handler Handler = new Handler();
    public static Random Random = new Random();

    public static string ConfigRoot { get; private set; } = "/etc/";

    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || args.Contains("--local-config"))
            ConfigRoot = "./cfg/";

        ConfigReader.ReadFromFile(ConfigRoot + "arbiter.cfg");
        ConfigReader.ReadFromFile(ConfigRoot + "mime.cfg");
        ConfigReader.ReadFromFile(ConfigRoot + "sites.cfg");

        SetPorts();

        Receiver.Requested += Receiver_Requested;
        Listener.OnConnection += Listener_OnConnection;

        Listener.Start();

        UpdateCerts();

        while (true)
            Thread.Sleep(-1);
    }

    private static void Listener_OnConnection(object sender, System.Net.Sockets.Socket socket)
    {
        Receiver.ReceiveOn(socket);
    }

    private static void Receiver_Requested(object sender, State state, Request request)
    {
        Handler.Handle(request).ContinueWith(async (task) =>
        {
            await Receiver.Reply(state, request, task.Result);
        });
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
            Process.Start("./acme.sh", $"--issue -d {host} -w {site.Path} --home acme/").WaitForExit();
            Process.Start("openssl", $"pkcs12 -export -out pfx/{host}.pfx -inkey acme/{host}/{host}.key -in acme/{host}/fullchain.cer -passout pass:").WaitForExit();
        }
    }
}
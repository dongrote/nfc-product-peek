using PCSC;
using PCSC.Monitoring;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using TumblerTags.Model;

namespace TumblerTags.ViewModel;

public partial class MainPageViewModel : ObservableObject, IDisposable
{
    IContextFactory contextFactory;
    IMonitorFactory monitorFactory;
    ISCardMonitor sCardMonitor;

    [ObservableProperty]
    List<SmartCard> smartCards;

    public MainPageViewModel(IContextFactory smartCardContextFactory, IMonitorFactory smartCardMonitorFactory)
    {
        contextFactory = smartCardContextFactory;
        monitorFactory = smartCardMonitorFactory;
        Initialize();
    }

    public void Dispose() => sCardMonitor.Dispose();

    void Initialize()
    {

        string[] readerNames;
        using (var ctx = contextFactory.Establish(SCardScope.System))
        {
            readerNames = ctx.GetReaders();
        }

        sCardMonitor = monitorFactory.Create(SCardScope.System);

        sCardMonitor.CardInserted += new CardInsertedEvent(SCardMonitor_CardInserted);
        sCardMonitor.CardRemoved += new CardRemovedEvent(SCardMonitor_CardRemoved);

        sCardMonitor.Start(readerNames);
    }

    private void SCardMonitor_CardRemoved(object sender, CardStatusEventArgs e)
    {
        SmartCards = new List<SmartCard>();
    }

    private void SCardMonitor_CardInserted(object sender, CardStatusEventArgs e)
    {
        var serialNumber = ReadSerialNumber(e.ReaderName);
        SmartCards = new List<SmartCard>()
        {
            new (contextFactory) {
                ReaderName = e.ReaderName,
                SerialNumber = serialNumber,
                SerialNumberHexString = Convert.ToHexString(serialNumber),
                UserData = Encoding.UTF8.GetString(ReadUserData(e.ReaderName)),
                UserDataWriteRequest = string.Empty,
            },
        };
    }

    private byte[] ReadSerialNumber(string readerName)
    {
        using var ctx = contextFactory.Establish(SCardScope.System);
        using var reader = ctx.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
        return ReadBytes(reader, 0, 9);
    }

    private byte[] ReadUserData(string readerName)
    {
        List<byte> userData = new ();
        using var ctx = contextFactory.Establish(SCardScope.System);
        using var reader = ctx.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
        for (ushort block = 4; block < (540 / 4); block++)
        {
            var bytes = ReadBytes(reader, block);
            foreach (var b in bytes)
            {
                if (b == 0) return userData.ToArray();
                userData.Add(b);
            }
        }

        return userData.ToArray();
    }

    private byte[] ReadBytes(ICardReader reader, ushort blockNumber, byte rxCount = 0x10)
    {
        const byte maxReadCount = 0x10;
        var cappedRxCount = Math.Min(rxCount, maxReadCount);
        var rx = new byte[cappedRxCount + 2];
        try
        {
            var apduBytes = new byte[]
            {
                0xff, // read class
                0xb0, // instruction
                (byte)((blockNumber >> 8) & 0xff), // block num (P1)
                (byte)(blockNumber & 0xff), // block num (P2)
                cappedRxCount, // number of bytes to read (max 16)
            };

            var rxByteCount = reader.Transmit(apduBytes, rx);
            return rx.Take(Range.EndAt(rxByteCount - 2)).ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new byte[0];
        }
    }
}

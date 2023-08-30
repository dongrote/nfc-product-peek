using PCSC;
using System.Diagnostics;
using System.Text;

namespace TumblerTags.Model;

public partial class SmartCard : ObservableObject
{
    IContextFactory contextFactory;

    [ObservableProperty]
    byte[] serialNumber;

    [ObservableProperty]
    string serialNumberHexString;

    [ObservableProperty]
    string readerName;

    [ObservableProperty]
    string userData;

    [ObservableProperty]
    string userDataWriteRequest;

    public SmartCard(IContextFactory smartCardcontextFactory)
    {
        contextFactory = smartCardcontextFactory;
    }

    [RelayCommand]
    void WriteUserData()
    {
        WriteUserData(ReaderName, UserDataWriteRequest);
        RefreshUserData();
    }

    [RelayCommand]
    void RefreshUserData()
    {
        UserData = Encoding.UTF8.GetString(ReadUserData(ReaderName));
    }

    private byte[] ReadUserData(string readerName)
    {
        List<byte> userData = new();
        using var ctx = contextFactory.Establish(SCardScope.System);
        using var reader = ctx.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
        for (byte block = 4; block < (540 / 4); block++)
        {
            var bytes = ReadBytes(reader, block, 4);
            foreach (var b in bytes)
            {
                if (b == 0) return userData.ToArray();
                userData.Add(b);
            }
        }

        return userData.ToArray();
    }

    private byte[] ReadBytes(ICardReader reader, byte blockNumber, byte rxCount = 0x10)
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
                0x00, // block num (P1)
                blockNumber, // block num (P2)
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

    private void WriteUserData(string readerName, string userData)
    {
        using var ctx = contextFactory.Establish(SCardScope.System);
        using var reader = ctx.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
        List<byte> bytes = new ();
        bytes.AddRange(Encoding.UTF8.GetBytes(userData));
        bytes.Add(0x00); // add null terminator
        WriteBytes(reader, 0x4, bytes.ToArray());
    }

    private void WriteBytes(ICardReader reader, byte blockNumber, byte[] bytes)
    {
        const int blockSize = 4;
        for (int i = 0; i <= (bytes.Length / blockSize); i++)
        {
            var rx = new byte[2];
            try
            {
                List<byte> packet = new();
                var slice = bytes.Take(new Range(i * blockSize, (i + 1) * blockSize)).ToArray();
                var apduBytes = new byte[]
                {
                    0xff,
                    0xd6,
                    0x00,
                    Convert.ToByte(blockNumber + i),
                    blockSize,
                };
                packet.AddRange(apduBytes);
                packet.AddRange(slice);
                for (int j = 0; j < (blockSize - slice.Length); j++) packet.Add(0);

                var rxByteCount = reader.Transmit(packet.ToArray(), rx);
                if (rxByteCount < 2)
                {
                    throw new Exception("Write failed.");
                }

                var statusCode = Convert.ToUInt16((rx[0] << 8) | rx[1]);
                if (statusCode != 0x9000)
                {
                    throw new Exception($"Write failed; status code 0x{statusCode.ToString("X")}");
                }

                Debug.WriteLine($"Status Code 0x{statusCode.ToString("X")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}

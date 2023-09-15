using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;


namespace PcxLibrary;

// Třída pro reprezentaci celé struktury PCX hlavičky
public class PcxHeader
{
    public byte Identifier { get; set; }
    public byte Version { get; set; }
    public byte Encoding { get; set; }
    public byte BitsPerPixel { get; set; }
    public ushort XStart { get; set; }
    public ushort YStart { get; set; }
    public ushort XEnd { get; set; }
    public ushort YEnd { get; set; }
    public ushort HorizontalResolution { get; set; }
    public ushort VerticalResolution { get; set; }
    public byte[/* 48 */]? Palette { get; set; }
    public byte Reserved1 { get; set; }
    public byte NumberOfBitPlanes { get; set; }
    public ushort BytesPerScanLine { get; set; }
    public ushort PaletteType { get; set; }
    public ushort HorizontalScreenSize { get; set; }
    public ushort VerticalScreenSize { get; set; }
    public byte[/* 54 */]? Reserverd2 { get; set; }
    public int Width => XEnd - XStart + 1;
    public int Height => YEnd - YStart + 1;
}

// Argumenty pro událost DecodingProgress
public class DecodingProgressEventArgs : EventArgs
{
    public int Progress { get; init; }
    public Image<Rgba32>? Image { get; init; } = null;
}

// Delegát pro událost DecodingProgress (informace o průběhu dekódování obrázku)
public delegate void DecodingProgressEventHandler(object sender, DecodingProgressEventArgs a);

// PCX dekodér
public class PcxDecoder
{
    // TODO: Vlastnost IsPcxFile musí vracet true pouze v případě, že podle header.Identifier se jedná o PCX obrázek.
    public bool IsPcxFile => header.Identifier == 10;
    // Událost slouží pro oznamování postupu při asynchronním dekódování obrázku.
    public event DecodingProgressEventHandler? DecodingProgress;
    // Vlastnost Image pro zpřístupnění obrázku po synchronním dékódování obrázku.
    public Image<Rgba32>? Image => image;
    // Vlastnost pro zpřístupnění hlavičky PCX obrázku
    public PcxHeader Header => header;

    // BinaryReader pro práci se vstupními daty
    private BinaryReader reader;
    // Dekódovaná hlavička obrázku
    private PcxHeader header;
    // Dekódovaný obrázek
    private Image<Rgba32>? image;

    // TODO: pro zpracování obrázků s rozšířenou 256 barevnou paletou bude třeba atribut pro zaznamenání palety
    public Rgba32[/* 256 */]? ExtendedPalette { get; set; }

    public PcxDecoder(Stream stream)
    {
        reader = new BinaryReader(new BufferedStream(stream, 4096));
        header = new();
    }

    protected virtual void OnDecodingProgress(DecodingProgressEventArgs args)
    {
        DecodingProgress?.Invoke(this, args);
    }

    public void ReadHeader()
    {
        // TODO: Načtěte kompletní hlavičku PCX obrázku do atributu header
        try
        {
            header.Identifier = reader.ReadByte();
            header.Version = reader.ReadByte();
            header.Encoding = reader.ReadByte();
            header.BitsPerPixel = reader.ReadByte();
            header.XStart = reader.ReadUInt16();
            header.YStart = reader.ReadUInt16();
            header.XEnd = reader.ReadUInt16();
            header.YEnd = reader.ReadUInt16();
            header.HorizontalResolution = reader.ReadUInt16();
            header.VerticalResolution = reader.ReadUInt16();
            header.Palette = new byte[48];
            for (int i = 0; i < 48; i++)
            {
                header.Palette[i] = reader.ReadByte();
            }
            header.Reserved1 = reader.ReadByte();
            header.NumberOfBitPlanes = reader.ReadByte();
            header.BytesPerScanLine = reader.ReadUInt16();
            header.PaletteType = reader.ReadUInt16();
            header.HorizontalScreenSize = reader.ReadUInt16();
            header.VerticalScreenSize = reader.ReadUInt16();
            header.Reserverd2 = new byte[54];
            for (int i = 0; i < 54; i++)
            {
                header.Reserverd2[i] = reader.ReadByte();
            }
        }
        catch (EndOfStreamException e)
        {

        }
    }

    public void DecodeImageInForegroundThread()
    {
        image = new Image<Rgba32>(header.Width, header.Height);

        DecodeImageInternal();
    }

    public void DecodeImageInBackgroundThread()
    {
        image = new Image<Rgba32>(header.Width, header.Height);

        Thread workerThread = new(DecodeImageInternal)
        {
            IsBackground = true
        };
        workerThread.Start();
    }

    private void DecodeImageInternal()
    {
        // TODO: Dokončete načítání obrazových dat do atributu image
        // - po načtení každého řádku obrazových dat vyvolejte událost DecodingProgress a předejte Progress (0-100 %)
        // - po dokončení načítání celého obrázku vyvolejte událost DecodingProgress (Progress = 100, Image = image)
        // - celá tato metoda pracuje synchronně a blokuje vlákno až do dokončení dekódování celého obrázku - nevytvářejte zde žádná vlákna, Tasky nebo jiné paralelizační objekty
        // - obrázek lze vyplnit pomocí: image[x, y] = new Rgba32(hodnotaRkanalu, hodnotaGkanalu, hodnotaBkanalu, 255);
        if (header.NumberOfBitPlanes == 3)
        {
            reader.BaseStream.Position = 128;
            for (int i = 0; i < header.Height; i++)
            {
                //nacteni radku
                byte[] buffer = scanLine();

                for (int j = 0; j < header.Width; j++)
                {
                    //tvorba obrazku
                    image[j, i] = new Rgba32(buffer[j], buffer[j + header.BytesPerScanLine], buffer[j + (2 * header.BytesPerScanLine)], 255);
                }
                OnDecodingProgress(new DecodingProgressEventArgs() { Progress = (int)((i / header.Height) * 100) });
            }
            OnDecodingProgress(new DecodingProgressEventArgs() { Image = image, Progress = 100 });
        }
        else
        {
            // TODO: Pokud se jedná o obrázek s rozšířenou 256 barevnou paletou, tak před vlastním dekódováním obrazových dat je nutné provést načtení palety
            //256 paleta
            reader.BaseStream.Position = reader.BaseStream.Length - 768;
            ExtendedPalette = new Rgba32[256];
            for (int i = 0; i < 256; i++)
            {
                ExtendedPalette[i] = new Rgba32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            }

            //bitmapa
            reader.BaseStream.Position = 128;
            byte[,] bitmapa = new byte[header.Width, header.Height];
            for (int i = 0; i < header.Height; i++)
            {
                byte[] scanedLine = scanLine();
                for (int j = 0; j < header.Width; j++)
                {
                    bitmapa[j, i] = scanedLine[j];
                }
            }

            //vytvoreni obrazku
            for (int i = 0; i < header.Height; i++)
            {
                for (int j = 0; j < header.Width; j++)
                {
                    image[j, i] = ExtendedPalette[bitmapa[j, i]];
                }
                OnDecodingProgress(new DecodingProgressEventArgs() { Progress = (int)((i / header.Height) * 100) });
            }
            OnDecodingProgress(new DecodingProgressEventArgs() { Image = image, Progress = 100 });
        }
    }

    //metoda na nacteni radku
    private byte[] scanLine()
    {
        int scanLineLength = (header.BytesPerScanLine * header.NumberOfBitPlanes);
        byte[] scanLine = new byte[scanLineLength];

        int index = 0;
        int runcount;

        do
        {
            runcount = 0;
            byte runvalue = 0;

            byte b = reader.ReadByte();
            if ((b & 0xC0) == 0xC0)
            {
                runcount = b & 0x3F;
                runvalue = reader.ReadByte();
            }
            else
            {
                runcount = 1;
                runvalue = b;
            }

            for (int i = runcount; runcount > 0; runcount--, index++)
            {
                scanLine[index] = runvalue;
            }
        } while (index < scanLine.Length);
        return scanLine;
    }
}

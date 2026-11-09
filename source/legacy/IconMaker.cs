using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ResolutionSwitcher;

internal static class IconMaker
{
    private static void Main(string[] args)
    {
        int[] sizes = {16,20,24,32,48,64,128,256};
        byte[][] frames = new byte[sizes.Length][];
        for (int i=0;i<sizes.Length;i++)
        {
            int size=sizes[i];
            using (Bitmap large = new Bitmap(size*3,size*3,PixelFormat.Format32bppPArgb))
            {
                using (Graphics graphics=Graphics.FromImage(large))
                    Brand.DrawBadge(graphics,new RectangleF(size*.06f,size*.06f,size*2.88f,size*2.88f));
                using (Bitmap bitmap=Synthwave.Downsample(large,size,size)) frames[i]=Dib(bitmap);
            }
        }
        using (BinaryWriter writer = new BinaryWriter(File.Create(args.Length>0 ? args[0] : "Lucent.ico")))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++)
            {
                writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)(sizes[i]==256?0:sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(frames[i].Length); writer.Write(offset); offset+=frames[i].Length;
            }
            foreach(byte[] frame in frames) writer.Write(frame);
        }
    }
    private static byte[] Dib(Bitmap bitmap)
    {
        int size=bitmap.Width, row=((size+31)/32)*4;
        using(MemoryStream stream=new MemoryStream())
        using(BinaryWriter writer=new BinaryWriter(stream))
        {
            writer.Write(40); writer.Write(size); writer.Write(size*2); writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(0); writer.Write(size*size*4); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for(int y=size-1;y>=0;y--) for(int x=0;x<size;x++)
            {
                Color pixel=bitmap.GetPixel(x,y); writer.Write(pixel.B); writer.Write(pixel.G); writer.Write(pixel.R); writer.Write(pixel.A);
            }
            for(int y=size-1;y>=0;y--)
            {
                byte[] mask=new byte[row];
                for(int x=0;x<size;x++) if(bitmap.GetPixel(x,y).A==0) mask[x/8]|=(byte)(128>>(x%8));
                writer.Write(mask);
            }
            return stream.ToArray();
        }
    }
}

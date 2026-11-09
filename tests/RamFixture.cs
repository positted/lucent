using System;
using System.IO;
using System.Threading;
internal static class RamFixture
{
    static void Main(string[] args){byte[] data=new byte[64*1024*1024];for(int i=0;i<data.Length;i+=4096)data[i]=1;File.WriteAllText(Path.Combine(args[0],"ready.txt"),"ready");while(!File.Exists(Path.Combine(args[0],"stop.txt")))Thread.Sleep(25);GC.KeepAlive(data);}
}

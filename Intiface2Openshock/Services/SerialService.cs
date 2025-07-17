using System.IO.Ports;

namespace Intiface2Openshock.Services;

public sealed class SerialService : IDisposable
{
    
    public SerialService()
    {
        
    }

    public string[] GetSerialPorts()
    {
        return SerialPort.GetPortNames();
    }
    
    public void Dispose()
    {
           
    }
}
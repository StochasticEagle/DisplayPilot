namespace DisplayPilot.Display.Interfaces;

public interface IMonitorData
{
    string Id { get; set; }
    string Name { get; set; }
    int MonitorNumber { get; set; }
    int Brightness { get; set; }
    int Contrast { get; set; }
    int Volume { get; set; }
    int ColorTemperatureVcp { get; set; }
    int Orientation { get; set; }
}

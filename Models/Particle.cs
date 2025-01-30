using System.Windows.Shapes;

namespace WDN.Models;

public class Particle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double VelocityX { get; set; }
    public double VelocityY { get; set; }
    public double Size { get; set; }
    public double Opacity { get; set; }
    public Shape? Shape { get; set; }
}
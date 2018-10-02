using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ExampleApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        class DataPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Angle { get; set; }
            public double Radius { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();

            var data1 = CreateSampleData(0);
            var data2 = CreateSampleData(Math.PI / 4.0);

            graph.AddData(data1, p => p.X, p => p.Y, Colors.Blue, "Example data 1", true, true);
            graph.AddData(data2, p => p.X, p => p.Y, Colors.Red, "Example data 2", true, true);
            graph.Update(true);
        }

        private static List<DataPoint> CreateSampleData(double offset)
        {
            var data = new List<DataPoint>();
            var pi = Math.PI;

            for (double i=-pi; i< pi; i = i+0.01)
            {
                var P = new DataPoint
                {
                    X = i,
                    Y = Math.Sin((i - offset) * 2) + 0.005 * Math.Sin(i*100),
                    Angle = i,
                    Radius = 1
                };
                data.Add(P);
            }
            return data;
        }


    }
}

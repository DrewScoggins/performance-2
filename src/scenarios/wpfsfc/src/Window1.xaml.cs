using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using System.Text;
using System.Diagnostics.Tracing;
using System.Diagnostics.Eventing;



namespace ShowFormComplex2_2008
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>

    public partial class Window1 : System.Windows.Window
    {
        private static EventSource log = new EventSource("SimpleTraceLoggingProvider", EventSourceSettings.EtwSelfDescribingEventFormat);
        public delegate void ExitDelegate();
        public Window1()
        {
            // Event Name                                  	Time MSec	Process Name  	Rest  
            // Microsoft-Windows-WPF/WClientAppCtor        	2,278.857	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="2" 
            // Microsoft-Windows-WPF/WClientAppRun         	2,280.906	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="14" 
            // Microsoft-Windows-WPF/WClientAppRun         	2,280.939	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="14" 
            // Microsoft-Windows-WPF/WClientParseBaml/Start	2,310.317	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="2" URI="pack://application:,,,/Window1.xaml" 

            log.Write("CmdLine", new EventSourceOptions {Level=EventLevel.LogAlways, Opcode=EventOpcode.Info }, 
                    new { Args = "Before Component Initialization, " + log.Guid });
            InitializeComponent();
            // Somewhere in between the component logs
            // Event Name                                  	Time MSec	Process Name  	Rest  
            // Microsoft-Windows-WPF/WClientParseBaml/Start	2,502.574	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="14" URI="pack://application:,,,/wpfsfc;component/window1.xaml" 
            // Microsoft-Windows-WPF/WClientParseBaml/Stop 	2,797.590	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="2" DURATION_MSEC="295.015" URI="pack://application:,,,/wpfsfc;component/window1.xaml" 

            log.Write("CmdLine", new EventSourceOptions {Level=EventLevel.LogAlways, Opcode=EventOpcode.Info }, 
                    new { Args = "After Component Initialization" });
            
            Stream str = this.GetType().Assembly.GetManifestResourceStream("ShowFormComplex2_2008.Resources.toolBarButton.jpg");
            log.Write("CmdLine", new EventSourceOptions {Level=EventLevel.LogAlways, Opcode=EventOpcode.Info }, 
                    new { Args = "After Manifest Stream" });
            if (str != null)
            {
                JpegBitmapDecoder decoder = new JpegBitmapDecoder(str, BitmapCreateOptions.None, BitmapCacheOption.None);
                ImageSource src = decoder.Frames[0];
                toolBarImgButton.Source = src;
                toolBarImgButton2.Source = src;
            }
            else
            {
                
            }
            if (Environment.GetCommandLineArgs().Length > 1)
            {
                // Log event verbosity level and opcode
                // This event is only logged when the session verbosity level is verbose.
                log.Write("CmdLine", new EventSourceOptions {Level=EventLevel.LogAlways, Opcode=EventOpcode.Info }, 
                                 new { Args = "Before ExitMethod" });
                // Somewhere between here and sleeping
                // Event Name                                   	Time MSec	Process Name  	Rest  
                // Microsoft-Windows-WPF/WClientParseBaml/Stop  	2,797.913	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="2" URI="pack://application:,,,/Window1.xaml" 
                // Microsoft-Windows-WPF/WClientLayout/Start    	2,913.776	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="6" Id="3,737,684" source="HwndSource_SetLayoutSize" 
                // Microsoft-Windows-WPF/WClientMeasure/Start   	2,913.778	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="6" Id="3,737,684" 
                // Microsoft-Windows-WPF/WClientUcePresent/Start	3,086.935	wpfsfc (20528)	ThreadID="37,188" ProcessorNumber="5" Id="2,185,361,438,048" QPCCurrentTime="781,547,341,366" 
                // Microsoft-Windows-WPF/WClientUcePresent/Stop 	3,087.033	wpfsfc (20528)	ThreadID="37,188" ProcessorNumber="5" DURATION_MSEC="0.098" Id="2,185,361,438,048" QPCCurrentTime="781,547,342,346" 
                // Microsoft-Windows-WPF/WClientMeasure/Stop    	3,107.761	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="14" Count="1" 
                // Microsoft-Windows-WPF/WClientArrange/Start   	3,107.761	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="14" Id="3,737,684" 
                // Microsoft-Windows-WPF/WClientOnRender/Start  	3,108.778	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="14" Id="506" # This is the First Render Start
                // Microsoft-Windows-WPF/WClientOnRender/Stop   	3,111.054	wpfsfc (20528)	ThreadID="25,660" ProcessorNumber="14" DURATION_MSEC="2.276" Id="506" 
                
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new ExitDelegate(ExitMethod));
            }
        }

        public void ExitMethod()
        {
            log.Write("CmdLine", new EventSourceOptions {Level=EventLevel.LogAlways, Opcode=EventOpcode.Info }, 
                                 new { Args = "Sleeping" });
            System.Threading.Thread.Sleep(2000);
            log.Write("CmdLine", new EventSourceOptions {Level=EventLevel.LogAlways, Opcode=EventOpcode.Info }, 
                                 new { Args = "Exiting" });
            Environment.Exit(0);
        }

        
       

    }
}
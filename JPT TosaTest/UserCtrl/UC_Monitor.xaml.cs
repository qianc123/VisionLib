﻿using JPT_TosaTest.ViewModel;
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

namespace JPT_TosaTest.UserCtrl
{
    /// <summary>
    /// UC_Monitor.xaml 的交互逻辑
    /// </summary>
    public partial class UC_Monitor : UserControl
    {
        public UC_Monitor()
        {
            InitializeComponent();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            (rootGrid.DataContext as MonitorViewModel).ClickOutputCommand.Execute(((Grid)sender).Tag);
        }
    }
}

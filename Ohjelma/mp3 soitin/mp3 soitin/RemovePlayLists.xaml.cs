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
using System.Windows.Shapes;
using static mp3_soitin.MainWindow;

namespace mp3_soitin
{
    /// <summary>
    /// Interaction logic for RemovePlayLists.xaml
    /// </summary>
    public partial class RemovePlayLists : Window
    {
        public List<PlaylistItem> Playlists { get; set; }


        public RemovePlayLists(List<PlaylistItem> playlists)
        { 
            InitializeComponent();
            Playlists = playlists;
            if (playlists != null)
            {
                RemovePlaylistBox.ItemsSource = playlists;
            }
            
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Remove_Button_Click(object sender, RoutedEventArgs e)
        {
            if (RemovePlaylistBox.SelectedItem != null)
            {
                PlaylistItem selectedPlaylist = (PlaylistItem)RemovePlaylistBox.SelectedItem;
                Playlists.Remove(selectedPlaylist);
                RemovePlaylistBox.Items.Refresh();
            }
        }

        private void Accept_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Button_Click_Minimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Button_Click_Quit(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}

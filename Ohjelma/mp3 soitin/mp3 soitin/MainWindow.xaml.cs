using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MaterialDesignThemes;
using MaterialDesignThemes.Wpf;
//using NAudio.Wave;
using ManagedBass;
using static mp3_soitin.MainWindow;
using Newtonsoft.Json;
//Vaihdoin NAudiosta BASS.NET libraryn
//Koska oli bugi luuppauksen kanssa
///Jos aloitti trackin ja kesken vaihtoi loop statea vaikka esimerkiksi no loop => Loop current.
////Se sitten soittaisi sen trackin loppuun ja jäisi jumiin eikä kutsu PlaybackStopped() funktiota
///Ja rikkoo koko ohjelman
namespace mp3_soitin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<Track> playlist = new();
        private readonly DispatcherTimer? progressTimer;
        int stream;

        private int currentTrackIndex =-1;
        private bool isPlaying = true;

        private enum LoopState
        {
            NoLoop,
            LoopCurrent,
            LoopPlaylist
        }

        private LoopState currentLoopState = LoopState.NoLoop;
        private List<PlaylistItem> playlists = new();

        public class PlaylistData
        {
            public List<PlaylistItem>? Playlists { get; set; }
        }
        readonly string filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "playlists.json");

        public MainWindow()
        {
            InitializeComponent();
            Bass.Init();
            LoadPlaylistsFromJson(filePath);
            TrackProgress.Minimum = 0;

            progressTimer = new()
            {
                Interval = TimeSpan.FromMilliseconds(1) // Laittaa päivitys aikavälin
            };
            progressTimer.Tick += UpdateProgressBar; //Tämä päivittää kappaleen edistymistä progress baarissa
            PlaylistComboBox.ItemsSource = playlists;
            PlaylistComboBox.SelectedIndex = 0;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)     //Tämä ei jostain syystä toimi
        {
            Bass.Free();
            progressTimer?.Stop();
        }

        private void LoadMP3FilesAndUpdatePlaylist(string[] filePaths, List<Track> targetTracks)    //Tämä lataa mp3 tiedostot ja päivittää soittolistan
        {
            List<Track> tracksToAdd = new();

            foreach (string mp3FilePath in filePaths)
            {
                tracksToAdd.Add(new Track
                {
                    SongName = System.IO.Path.GetFileNameWithoutExtension(mp3FilePath),
                    FilePath = mp3FilePath
                });
            }
            playlist.Clear();
            playlist.AddRange(tracksToAdd);
            targetTracks.AddRange(tracksToAdd);

            PlaylistBox.ItemsSource = null; //Tyhjentää nykyisen soittolistalaatikon
            PlaylistBox.ItemsSource = targetTracks; //Asettaa soittolistalaatikolle kappaleet näkyviin
        }

        public class PlaylistItem
        {
            public string? Name { get; set; }
            public List<Track>? Tracks { get; set; }
        }

        private void CreateNewPlaylist(string playlistName, string[] filePaths) //Tämä tekee uuden soittolistan
        {
            var newPlaylistItem = new PlaylistItem
            {
                Name = playlistName,
                Tracks = new List<Track>()
            };

            LoadMP3FilesAndUpdatePlaylist(filePaths, newPlaylistItem.Tracks);

            EmptyPlaylistLabel.Visibility = Visibility.Hidden;  //Piiloittaa labelin jossa lukee "The playlist is empty"

            playlists.Add(newPlaylistItem);
            PlaylistComboBox.ItemsSource = null;    //Tyhjentää nykyisen soittolista ComboBoxin
            PlaylistComboBox.ItemsSource = playlists;   //Tämä lisää siihen soittolista ComboBoxiin soittolistat

            int newPlaylistIndex = playlists.IndexOf(newPlaylistItem);
            PlaylistComboBox.SelectedIndex = newPlaylistIndex;
        }
        
        public class Playlist
        {
            public string? Name { get; set; }
            public List<Track>? Tracks { get; set; }
        }


        public class Track
        {
            public string? SongName { get; set; }
            public string? FilePath { get; set; }
        }

        private void PlayTrack(Track track) //Tämä on sovelluksen ydin
        {
            if (track != null)
            {
                if (Bass.ChannelIsActive(stream) == PlaybackState.Playing)
                {
                    Bass.ChannelStop(stream);
                    Bass.StreamFree(stream);    //Tämä tyhjentää Soivan kappaleen jonka avulla ohjelman memory usage laskee
                }
                
                stream = Bass.CreateStream(track.FilePath);

                if (stream != 0)
                {
                    if (progressTimer != null)
                    {
                        progressTimer.Start();

                        Bass.ChannelSetSync(stream, SyncFlags.End, 0, (handle, channel, data, user) =>  //Tämä alkaa kun kappaleen soittaminen loppuu
                        {
                            if (currentLoopState == LoopState.LoopCurrent)          //Tämä looppaa saman kappaleen
                            {
                                Bass.ChannelSetPosition(stream, 0);
                                progressTimer.Start();
                                Bass.ChannelPlay(stream);

                            }
                            else if (currentLoopState == LoopState.LoopPlaylist)    //Tämä looppaa koko soittolistan
                            {
                                if (stream != 0 && Bass.ChannelIsActive(stream) != PlaybackState.Playing)
                                {
                                    MoveToNextTrack();
                                    ChangeTrack(playlist[currentTrackIndex]);
                                }
                            }
                            else
                            {
                                progressTimer.Stop();
                                isPlaying = !isPlaying;
                                Dispatcher.Invoke(() =>
                                {
                                    PlayPauseIcon.Kind = PackIconKind.Play;
                                });
                            }
                        }, IntPtr.Zero);
                    }

                    Bass.ChannelPlay(stream);                           //Tämä soittaa valitun kappaleen
                
                    currentTrackIndex = playlist.IndexOf(track);        //Edit: Ei ole. Onko tämä turha?
                }
            }
        }

        private void UpdateProgressBar(object? sender, EventArgs e)     //Tämä päivittää
        {
            if (Bass.ChannelIsActive(stream) == PlaybackState.Playing)
            {
                double progressPercentage = 0;

                long position = Bass.ChannelGetPosition(stream);        //Kappaleen nykyinen kohta
                long duration = Bass.ChannelGetLength(stream);          //Kappaleen pituus

                if (duration != 0)
                {
                    progressPercentage = (double)position / duration * 100;
                }

                TrackProgress.Value = progressPercentage;
                Dispatcher.Invoke(() => { TrackProgress.Value = progressPercentage; });
            }
            else    //Tämä nollaa 
            {
                TrackProgress.Value = 0;
                Dispatcher.Invoke(() => TrackProgress.Value );
            }
        }

        private void TrackProgress_MouseDown(object sender, MouseButtonEventArgs e) //Tämä hoitaa kun käyttäjä haluaa päästä tiettyyn kohtaan kappaleessa
        {
            if (Bass.ChannelIsActive(stream) == PlaybackState.Playing)
            {
                double progressBarWidth = TrackProgress.ActualWidth;
                double mouseX = e.GetPosition(TrackProgress).X;

                double newPlaybackPosition = (mouseX / progressBarWidth) * 100.0;
                long newTimeBytes = Bass.ChannelSeconds2Bytes(stream, newPlaybackPosition / 100.0 * Bass.ChannelBytes2Seconds(stream, Bass.ChannelGetLength(stream)));

                Bass.ChannelSetPosition(stream, newTimeBytes);                      //Tämä asettaa kappaleen nykyisen sijainnin
            }
        }

        private void MoveToNextTrack()          //Tästä olisi voinut tehdä yleisen MoveTrack funktion jota olisi voinut käyttää Playlist looppauksessa ja Next ja Previous napeissa
        {
            if (playlist.Count == 0)
            {
                return;
            }

            currentTrackIndex++;

            if (currentTrackIndex >= playlist.Count)
            {
                
                currentTrackIndex = 0;
            }
        }

        private void Play_Pause_Button_Click(object sender, RoutedEventArgs e)      //Tämä hoitaa kappaleen pysäytyksen ja jatkamisen
        {
            if (currentTrackIndex >= 0 && playlist.Count > 0)
            {
                if (currentTrackIndex < playlist.Count)
                {
                    if (isPlaying)
                    {
                        if (Bass.ChannelIsActive(stream) == PlaybackState.Playing)
                        {
                            Bass.ChannelStop(stream); // Tämä pysäyttää kappaleen jos se soi kun painaa nappia
                        }
                        PlayPauseIcon.Kind = PackIconKind.Play;
                        PlayPauseButton.ToolTip = "Play";
                    }
                    else
                    {
                        if (Bass.ChannelIsActive(stream) == PlaybackState.Paused || Bass.ChannelIsActive(stream) == PlaybackState.Stopped)
                        {
                            Bass.ChannelPlay(stream); // Tämä sitten taas jatkaa kappaletta jos se on pysäytetty kun painaa nappia
                        }
                        else
                        {
                            PlayTrack(playlist[currentTrackIndex]);
                        }

                        PlayPauseIcon.Kind = PackIconKind.Pause;
                        PlayPauseButton.ToolTip = "Pause";
                    }
                    isPlaying = !isPlaying;
                }   
            }
        }
        
        private void ChangeTrack(Track track)       //Tämä vaihaa kappaleen annettuun kappaleeseen
        {
            if (track != null)
            {
                progressTimer?.Stop();
                currentTrackIndex = playlist.IndexOf(track);
                if (isPlaying) 
                {
                    PlayTrack(track);
                }

                if (!isPlaying)
                {
                    Bass.StreamFree(stream);        //Tyhjentää soivan kappaleen
                    PlayTrack(track);               //Soittaa valitun kappaleen
                    Bass.ChannelStop(stream);       //Pysäyttää sen
                }
                Dispatcher.Invoke(() =>             //Tämä asettaa uuden kappaleen nimen TitleBoxiin
                {
                    TrackTitlebox.Text = track.SongName;
                });
            }
        }

        private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)   //Tämä
        {
            if (PlaylistBox.SelectedItem is Track selectedTrack)
            {
                ChangeTrack(selectedTrack);
            }
        }

        private void Next_Button_Click(object sender, RoutedEventArgs e)        //Tämä hoitaa kappaleen vaihtamisen kun painaa next button
        {
            if (stream != 0 && playlist.Count > 0)
            {
                MoveToNextTrack();
                ChangeTrack(playlist[currentTrackIndex]);
            }
        }

        private void Previous_Button_Click(object sender, RoutedEventArgs e)    //Tämä hoitaa kappaleen vaihtamisen kun painaa previous button
        {
            if (currentTrackIndex >= 0 && playlist.Count > 0)
            {
                Bass.ChannelSetPosition(stream, 0);

                currentTrackIndex--;
                if (currentTrackIndex < 0)
                {
                    currentTrackIndex = playlist.Count - 1;
                    ChangeTrack(playlist[currentTrackIndex]);
                }
                else
                {
                    ChangeTrack(playlist[currentTrackIndex]);
                }
            }
        }
        
        private void Button_Click_Quit(object sender, RoutedEventArgs e)    //Tämä hoitaa ohjelman sulkemisen ja soittolistojen tallentamisen
        {
            SavePlaylistsToJson(filePath);      //Laitoin nämä tänne koska tuntuu että sen MainWindow_Closing() Ei toiminut
            Bass.Free();                        //Tyhjentää Bass soittimen.
            Close();
        }

        private void Button_Click_Minimize(object sender, RoutedEventArgs e)    //Tämä minimisoi ikkunan kun painaa nappia
        {
            WindowState = WindowState.Minimized;
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)          //Tällä pysyy siirtämään mp3 soitin ohjelmaa
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Newplaylist_Click(object sender, RoutedEventArgs e)        //Tämä hoitaa uuden soittolistan tekemisen
        {
            var newPlaylistDialog = new NewPlayListName();
            if (newPlaylistDialog.ShowDialog() == true)                         //Tämä avaa uuden ikkunan nimeltä 
            {
                string newPlaylistName = newPlaylistDialog.PlaylistNameTextBox.Text;

                if (string.IsNullOrEmpty(newPlaylistName))                      //Tämä tarkistaa onko uudelle soittolistalle annettu nimi tyhjä
                {
                    System.Windows.MessageBox.Show("Please enter a valid playlist name.", "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                OpenFileDialog openFileDialog = new()                           //Tämä avaa valikon jossa valitaan mp3 tiedostot
                {
                    Title = "Select MP3 Files",
                    Filter = "MP3 Files|*.mp3|All Files|*.*",
                    Multiselect = true
                };

                DialogResult result = openFileDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    string[] selectedFilePaths = openFileDialog.FileNames;
                    CreateNewPlaylist(newPlaylistName, selectedFilePaths);
                }
            }
        }

        private void LoopButton_Click(object sender, RoutedEventArgs e)     //Tämä nappi asettaa Loop staten. Eli Google kääntäjän mukaan silmukan tilan
        {
            switch (currentLoopState)
            {
                case LoopState.NoLoop:
                    currentLoopState = LoopState.LoopCurrent;
                    LoopButton.ToolTip = "Loop current";
                    LoopButtonIcon.Kind = PackIconKind.RepeatOne;
                    break;
                case LoopState.LoopCurrent:
                    currentLoopState = LoopState.LoopPlaylist;
                    LoopButton.ToolTip = "Loop Playlist";
                    LoopButtonIcon.Kind = PackIconKind.RepeatVariant;
                    break;
                case LoopState.LoopPlaylist:
                    currentLoopState = LoopState.NoLoop;
                    LoopButton.ToolTip = "Looping disabled";
                    LoopButtonIcon.Kind = PackIconKind.RepeatOff;
                    break;
            }
        }

        private void PlaylistComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)  //Tämä hoitaa soittolistojen vaihtamisen
        {
            if (PlaylistComboBox.SelectedItem is PlaylistItem selectedPlaylist)
            {
                EmptyPlaylistLabel.Visibility = Visibility.Hidden;                                  //Tämä piiloittaa Labelin jossa lukee "The playlist is empty"

                playlist.Clear();//Tyhjentää nykyisen soittolistan
                
                if (selectedPlaylist.Tracks != null)        //Tarkistaa jos valitun soittolistan kappaleet eivät ole tyhjiä
                {
                    playlist.AddRange(selectedPlaylist.Tracks);     //Lisää kappaleet soittolistaan
                    PlaylistBox.ItemsSource = selectedPlaylist.Tracks;  //Laittaa kappaleet näkyviin Soittolista laatikkoon
                }
            }
        }

        private void LoadPlaylistsFromJson(string filePath)     //Tämä hoitaa tallennetun soittolistan lataamisen
        {
            if (File.Exists(filePath))                          //Jos tiedosto on olemassa
            {
                string jsonData = File.ReadAllText(filePath);   //Tämä lukee tiedostossa olevan tiedon

                var playlistData = JsonConvert.DeserializeObject<PlaylistData>(jsonData);   //Tämä muuttaa tiedostosta saadun tiedon

                if (playlistData != null && playlistData.Playlists != null)
                {
                    playlists = playlistData.Playlists;         //Asettaa tiedostosta saadun tiedon variableen playlists
                }
            }
        }

        private void SavePlaylistsToJson(string filePath)       //Tämä hoitaa soittolistojen tallentamisen
        {
            var playlistData = new PlaylistData
            {
                Playlists = playlists
            };

            string jsonData = JsonConvert.SerializeObject(playlistData);    //Muuttaa variablen playlists json dataksi

            File.WriteAllText(filePath, jsonData);                          //Tallentaa tiedon. Joka on käyttäjän tehdyt soittolistat
        }

        private void RemovePlaylist_Click(object sender, RoutedEventArgs e)     //Tämä hoitaa soittolistojen poistamisen
        {
            PlaylistItem selectedPlaylist = (PlaylistItem)PlaylistComboBox.SelectedItem;

            RemovePlayLists RemovePlayListwindow = new(playlists);
            if (RemovePlayListwindow.ShowDialog() == true)                      //Avaa soittolistojen poistamis- ikkunan
            {
                playlists = RemovePlayListwindow.Playlists;                     //Asettaa playlists variableen RemovePlayListwindow ikkunasta saadut uudet soittolistat

                if (selectedPlaylist != null && !playlists.Contains(selectedPlaylist))  //Tämä tarkistaa jos nykyinen soittolista poistetaan
                {
                    playlist.Clear();
                    if (Bass.ChannelIsActive(stream) == PlaybackState.Playing)          //Tämä pysäyttää audion jos se soi. Taitaa olla vähän turha kun heti seuraavaksi tyhjennetään Bass.Stream
                    {
                        Bass.ChannelStop(stream);   //Tämä pysäyttää soivan kappaleen
                    }
                    Bass.StreamFree(stream);    //Tämä tyhjentää soittimen
                    stream = 0;
                    Dispatcher.Invoke(() =>
                    {
                        TrackTitlebox.Text = "Track not selected";
                    });
                }

                PlaylistComboBox.ItemsSource = null;        //Tyhjentää ComboBox listan
                PlaylistBox.ItemsSource = null;             //Tyhjentää soittolista laatikon

                if (playlists != null && playlists.Count > 0)
                {
                    PlaylistComboBox.ItemsSource = playlists;   //Asettaa soittolistast PlaylistComboBoxiin.
                    PlaylistComboBox.SelectedIndex = 0;         //Asettaa valitun soittolistan valmiiksi
                }
                else    //Jos playlists on tyhjä eli jos ei ole enää soittolistoja
                {
                    playlist.Clear();   //Tämä tyhjentää soittolistan
                    EmptyPlaylistLabel.Visibility = Visibility.Visible;
                    Dispatcher.Invoke(() =>
                    {
                        TrackTitlebox.Text = "Track not selected";
                    });
                }    
                return;
            }
        }
    }
}
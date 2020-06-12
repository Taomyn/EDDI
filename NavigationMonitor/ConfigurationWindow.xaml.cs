﻿using Eddi;
using EddiConfigService;
using EddiDataDefinitions;
using EddiNavigationService;
using EddiShipMonitor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace EddiNavigationMonitor
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class ConfigurationWindow : UserControl
    {
        private NavigationMonitorConfiguration navConfig = new NavigationMonitorConfiguration();
        private string searchTypeSelection = String.Empty;
        private string searchQuerySelection = String.Empty;

        private readonly List<string> searchType = new List<string> {
            "crime",
            "missions",
            "services",
            "ship"
        };

        private readonly List<string> searchQuery = new List<string> {
            "cancel",
            "encoded",
            "expiring",
            "farthest",
            "guardian",
            "human",
            "facilitator",
            "manufactured",
            "most",
            "nearest",
            "next",
            "raw",
            "route",
            "scoop",
            "set",
            "source",
            "update"
        };

        private NavigationMonitor navigationMonitor()
        {
            return (NavigationMonitor)EDDI.Instance.ObtainMonitor("Navigation monitor");
        }

        public ConfigurationWindow()
        {
            InitializeComponent();
            bookmarksData.ItemsSource = navigationMonitor()?.bookmarks;

            ConfigureSearchTypeOptions();
            searchTypeDropDown.SelectedItem = Properties.NavigationMonitor.search_type_missions;
            searchQueryDropDown.SelectedItem = Properties.NavigationMonitor.search_query_missions_route;

            navConfig = ConfigService.Instance.navigationMonitorConfiguration;
            maxSearchDistanceInt.Text = navConfig.maxSearchDistanceFromStarLs?.ToString(CultureInfo.InvariantCulture);
        }

        private void addBookmark(object sender, RoutedEventArgs e)
        {
            navConfig = ConfigService.Instance.navigationMonitorConfiguration;

            Bookmark bookmark = new Bookmark(Properties.NavigationMonitor.blank_bookmark, null, null, null, null, false);
            navConfig.bookmarks.Add(bookmark);
            ConfigService.Instance.navigationMonitorConfiguration = navConfig;
        }

        private void removeBookmark(object sender, RoutedEventArgs e)
        {
            navConfig = ConfigService.Instance.navigationMonitorConfiguration;

            Bookmark bookmark = (Bookmark)((Button)e.Source).DataContext;
            navigationMonitor()._RemoveBookmark(bookmark);
            navConfig.bookmarks = navigationMonitor()?.bookmarks;
            ConfigService.Instance.navigationMonitorConfiguration = navConfig;
        }

        private void updateBookmark(object sender, RoutedEventArgs e)
        {

        }

        private void setCourse(object sender, RoutedEventArgs e)
        {

        }

        private void prioritizeOrbitalStationsEnabled(object sender, RoutedEventArgs e)
        {
            updateCheckbox();
        }

        private void prioritizeOrbitalStationsDisabled(object sender, RoutedEventArgs e)
        {
            updateCheckbox();
        }

        private void updateCheckbox()
        {
            navConfig = ConfigService.Instance.navigationMonitorConfiguration;

            bool isChecked = prioritizeOrbitalStations.IsChecked.Value;
            if (navConfig.prioritizeOrbitalStations != isChecked)
            {
                navConfig.prioritizeOrbitalStations = isChecked;
                ConfigService.Instance.navigationMonitorConfiguration = navConfig;
            }
        }

        private void maxSearchDistance_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                maxStationDistance_Changed();
            }
        }

        private void maxSearchDistance_LostFocus(object sender, RoutedEventArgs e)
        {
            maxStationDistance_Changed();
        }

        private void maxStationDistance_Changed()
        {
            navConfig = ConfigService.Instance.navigationMonitorConfiguration;

            try
            {
                int? distance = string.IsNullOrWhiteSpace(maxSearchDistanceInt.Text)
                    ? 10000 : Convert.ToInt32(maxSearchDistanceInt.Text, CultureInfo.InvariantCulture);
                if (distance != navConfig.maxSearchDistanceFromStarLs)
                {
                    navConfig.maxSearchDistanceFromStarLs = distance;
                    ConfigService.Instance.navigationMonitorConfiguration = navConfig;
                }
            }
            catch
            {
                // Bad user input; ignore it
            }
        }

        private void ConfigureSearchTypeOptions()
        {
            List<string> SearchTypeOptions = new List<string>();

            foreach (string type in searchType)
            {
                SearchTypeOptions.Add(Properties.NavigationMonitor.ResourceManager.GetString("search_type_" + type));
            }
            searchTypeDropDown.ItemsSource = SearchTypeOptions;
        }

        private void searchTypeDropDownUpdated(object sender, SelectionChangedEventArgs e)
        {
            foreach (string type in searchType)
            {
                string property = Properties.NavigationMonitor.ResourceManager.GetString("search_type_" + type);
                if (property == searchTypeDropDown.SelectedItem?.ToString())
                {
                    searchTypeSelection = type;
                    break;
                }
            }
            ConfigureSearchQueryOptions(searchTypeSelection);

            // Set the default query
            switch (searchTypeSelection)
            {
                case "crime":
                    {
                        searchQueryDropDown.SelectedItem = Properties.NavigationMonitor.search_query_crime_facilitator;
                    }
                    break;
                case "missions":
                    {
                        searchQueryDropDown.SelectedItem = Properties.NavigationMonitor.search_query_missions_route;
                    }
                    break;
                case "services":
                    {
                        searchQueryDropDown.SelectedItem = Properties.NavigationMonitor.search_query_services_encoded;
                    }
                    break;
                case "ship":
                    {
                        searchQueryDropDown.SelectedItem = Properties.NavigationMonitor.search_query_ship_scoop;
                    }
                    break;
            }
        }

        private void ConfigureSearchQueryOptions(string type)
        {
            List<string> SearchQueryOptions = new List<string> {};

            foreach (string query in searchQuery)
            {
                string property = Properties.NavigationMonitor.ResourceManager.GetString("search_query_"
                    + type + "_" + query);
                if (property != null) { SearchQueryOptions.Add(property); }
            }
            searchQueryDropDown.ItemsSource = SearchQueryOptions;
        }

        private void searchQueryDropDownUpdated(object sender, SelectionChangedEventArgs e)
        {
            bool found = false;
            foreach (string type in searchType)
            {
                foreach (string query in searchQuery)
                {
                    string property = Properties.NavigationMonitor.ResourceManager.GetString("search_query_"
                        + type + "_" + query);
                    if (property == searchQueryDropDown.SelectedItem?.ToString())
                    {
                        searchQuerySelection = query;
                        found = true;
                        break;
                    }
                }
                if (found) { break; }
            }
        }

        private void executeSearch(object sender, RoutedEventArgs e)
        {
            Button searchButton = (Button)sender;
            searchButton.Foreground = Brushes.Red;
            searchButton.FontWeight = FontWeights.Bold;
            string searchSystem = null;
            
            Thread searchThread = new Thread(() =>
            {
                if (searchTypeSelection == "crime")
                {
                    switch (searchQuerySelection)
                    {
                        case "facilitator":
                            {
                                searchSystem = NavigationService.Instance.GetServiceRoute(searchQuerySelection);
                            }
                            break;
                    }
                }
                else if (searchTypeSelection == "missions")
                {
                    switch (searchQuerySelection)
                    {
                        case "expiring":
                            {
                                searchSystem = NavigationService.Instance.GetExpiringRoute();
                            }
                            break;
                        case "farthest":
                            {
                                searchSystem = NavigationService.Instance.GetFarthestRoute();
                            }
                            break;
                        case "most":
                            {
                                searchSystem = NavigationService.Instance.GetMostRoute();
                            }
                            break;
                        case "nearest":
                            {
                                searchSystem = NavigationService.Instance.GetNearestRoute();
                            }
                            break;
                        case "next":
                            {
                                searchSystem = NavigationService.Instance.GetNextInRoute();
                            }
                            break;
                        case "route":
                            {
                                searchSystem = NavigationService.Instance.GetMissionsRoute();
                            }
                            break;
                        case "source":
                            {
                                searchSystem = NavigationService.Instance.GetSourceRoute();
                            }
                            break;
                        case "update":
                            {
                                searchSystem = NavigationService.Instance.UpdateRoute();
                            }
                            break;
                    }
                }
                else if (searchTypeSelection == "services")
                {
                    searchSystem = NavigationService.Instance.GetServiceRoute(searchQuerySelection);
                }
                else if (searchTypeSelection == "ship")
                {
                    switch (searchQuerySelection)
                    {
                        case "cancel":
                            {
                                NavigationService.Instance.CancelRoute();
                            }
                            break;
                        case "scoop":
                            {
                                ShipMonitor.JumpDetail detail = ((ShipMonitor)EDDI.Instance.ObtainMonitor("Ship monitor")).JumpDetails("total");
                                searchSystem = NavigationService.Instance.GetScoopRoute(detail.distance);
                            }
                            break;
                        case "set":
                            {
                                searchSystem = NavigationService.Instance.SetRoute();
                            }
                            break;
                    }

                }

                Dispatcher?.Invoke(() =>
                {
                    searchButton.Foreground = Brushes.Black;
                    searchButton.FontWeight = FontWeights.Regular;

                    // If 'search system' found, send to clipboard
                    if (searchSystem != null)
                    {
                        Clipboard.SetData(DataFormats.Text, searchSystem);
                    }
                });
            })
            {
                IsBackground = true
            };
            searchThread.Start();
        }

        private void searchSystemChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void searchSystemText_LostFocus(object sender, RoutedEventArgs e)
        {

        }
        private void searchStationChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void searchStationText_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        private void bookmarksUpdated(object sender, DataTransferEventArgs e)
        {
            // Update the cargo monitor's information
            navigationMonitor()?.writeBookmarks();
        }

        private void EnsureValidInteger(object sender, TextCompositionEventArgs e)
        {
            // Match valid characters
            Regex regex = new Regex(@"[0-9]");
            // Swallow the character doesn't match the regex
            e.Handled = !regex.IsMatch(e.Text);
        }
    }
}
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Settings;

public partial class SettingsPage : ContentPage,IHostedPage
{
 private readonly ISettingsService _service;private AppSettings _settings=new();
 private Picker _theme=null!,_language=null!,_date=null!,_currency=null!,_timeZone=null!;
 private Entry _accent=null!;private Switch _compact=null!,_notifications=null!,_autoSync=null!,_offline=null!,_remember=null!;private Label _error=null!;
 private static readonly string[] Themes=["System","Light","Dark"],Languages=["English","Hindi","Urdu"],LanguageCodes=["en","hi","ur"],Dates=["dd MMM yyyy","dd/MM/yyyy","MM/dd/yyyy","yyyy-MM-dd"],Currencies=["USD ($)","INR (₹)","EUR (€)","GBP (£)","AED (د.إ)"],CurrencyCodes=["USD","INR","EUR","GBP","AED"],CurrencySymbols=["$","₹","€","£","د.إ"],TimeZones=["UTC","Asia/Kolkata","Asia/Dubai","Europe/London","America/New_York"];
 public SettingsPage(ISettingsService service){InitializeComponent();_service=service;}
 protected override async void OnAppearing(){base.OnAppearing();await LoadForHostAsync();}
 public async Task LoadForHostAsync(){_settings=await _service.GetAsync();BuildContent();}
 private async void OnRefreshing(object s,EventArgs e){await LoadForHostAsync();Refresher.IsRefreshing=false;}
 private void BuildContent(){SettingsContent.Children.Clear();
  _theme=MakePicker(Themes,(int)_settings.Theme);_accent=new Entry{Text=_settings.AccentColor,HorizontalTextAlignment=TextAlignment.Center};_compact=new Switch{IsToggled=_settings.CompactMode};
  SettingsContent.Children.Add(Card("Appearance",Row("Theme",_theme),Row("Accent color",Wrap(_accent)),Row("Compact mode",_compact)));
  _language=MakePicker(Languages,Array.IndexOf(LanguageCodes,_settings.Language));_date=MakePicker(Dates,Array.IndexOf(Dates,_settings.DateFormat));_currency=MakePicker(Currencies,Array.IndexOf(CurrencyCodes,_settings.Currency));_timeZone=MakePicker(TimeZones,Array.IndexOf(TimeZones,_settings.TimeZone));
  SettingsContent.Children.Add(Card("Regional",Row("Language",_language),Row("Date format",_date),Row("Currency",_currency),Row("Time zone",_timeZone)));
  _notifications=new Switch{IsToggled=_settings.Notifications};_autoSync=new Switch{IsToggled=_settings.AutoSync};_offline=new Switch{IsToggled=_settings.OfflineMode};_remember=new Switch{IsToggled=_settings.RememberLogin};
  SettingsContent.Children.Add(Card("Sync & Preferences",Row("Notifications",_notifications),Row("Auto-sync",_autoSync),Row("Offline mode",_offline),Row("Remember login",_remember)));
  _error=new Label{TextColor=Color.FromArgb("#DC2626"),IsVisible=false,HorizontalTextAlignment=TextAlignment.Center};SettingsContent.Children.Add(_error);var save=new Button{Text="Save Settings",Style=(Style)Resources["PrimaryButton"],HorizontalOptions=LayoutOptions.Fill,MaximumWidthRequest=500};save.Clicked+=OnSaveClicked;SettingsContent.Children.Add(save);
 }
 private async void OnSaveClicked(object? s,EventArgs e){_settings.Theme=(ThemeMode)Math.Max(0,_theme.SelectedIndex);_settings.AccentColor=_accent.Text?.Trim()??"";_settings.CompactMode=_compact.IsToggled;_settings.Language=LanguageCodes[Math.Max(0,_language.SelectedIndex)];_settings.DateFormat=Dates[Math.Max(0,_date.SelectedIndex)];var ci=Math.Max(0,_currency.SelectedIndex);_settings.Currency=CurrencyCodes[ci];_settings.CurrencySymbol=CurrencySymbols[ci];_settings.TimeZone=TimeZones[Math.Max(0,_timeZone.SelectedIndex)];_settings.Notifications=_notifications.IsToggled;_settings.AutoSync=_autoSync.IsToggled;_settings.OfflineMode=_offline.IsToggled;_settings.RememberLogin=_remember.IsToggled;var result=await _service.SaveAsync(_settings);_error.Text=result.Ok?"Settings saved locally and queued for sync.":result.Error;_error.TextColor=result.Ok?Color.FromArgb("#059669"):Color.FromArgb("#DC2626");_error.IsVisible=true;if(result.Ok&&Microsoft.Maui.Controls.Application.Current is not null)Microsoft.Maui.Controls.Application.Current.UserAppTheme=_settings.Theme switch{ThemeMode.Dark=>AppTheme.Dark,ThemeMode.Light=>AppTheme.Light,_=>AppTheme.Unspecified};}
 private static Picker MakePicker(IEnumerable<string> values,int index)=>new(){ItemsSource=values.ToList(),SelectedIndex=index<0?0:index,HorizontalTextAlignment=TextAlignment.Center,WidthRequest=210};
 private static Border Wrap(View view)=>new(){Content=view,Stroke=Color.FromArgb("#CBD5E1"),StrokeThickness=1,Padding=new Thickness(8,0),WidthRequest=210};
 private static Grid Row(string label,View control){var g=new Grid{ColumnDefinitions=new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)),ColumnSpacing=12};g.Add(new Label{Text=label,VerticalOptions=LayoutOptions.Center},0,0);g.Add(control,1,0);return g;}
 private Border Card(string title,params View[] rows){var stack=new VerticalStackLayout{Spacing=12};stack.Children.Add(new Label{Text=title,FontSize=14,FontAttributes=FontAttributes.Bold});stack.Children.Add(new BoxView{HeightRequest=1,BackgroundColor=Color.FromArgb("#E2E8F0")});foreach(var row in rows)stack.Children.Add(row);return new Border{Style=(Style)Resources["AppCard"],Content=stack,MaximumWidthRequest=760};}
}

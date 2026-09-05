using System.Text.RegularExpressions;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalSettingsService:ISettingsService
{
 private static readonly HashSet<string> Languages=["en","hi","ur"];
 private static readonly IReadOnlyDictionary<string,string> Currencies=new Dictionary<string,string>{{"USD","$"},{"INR","₹"},{"EUR","€"},{"GBP","£"},{"AED","د.إ"}};
 private static readonly HashSet<string> DateFormats=["dd MMM yyyy","dd/MM/yyyy","MM/dd/yyyy","yyyy-MM-dd"];
 private readonly IDbContextFactory<LocalAppDbContext> _factory;private readonly IAppSessionContext _session;
 public LocalSettingsService(IDbContextFactory<LocalAppDbContext> factory,IAppSessionContext session){_factory=factory;_session=session;}
 public async Task<AppSettings> GetAsync(){if(_session.CurrentCompanyId is not Guid cid||_session.CurrentUserId is not Guid uid)return new AppSettings();await using var db=await _factory.CreateDbContextAsync();var x=await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s=>s.CompanyId==cid&&s.UserId==uid);return x is null?new AppSettings{Id=Guid.NewGuid(),CompanyId=cid,UserId=uid,LastCompanyId=cid}:Map(x);}
 public async Task<(bool Ok,string? Error)> SaveAsync(AppSettings m){var error=Validate(m);if(error is not null)return(false,error);await using var db=await _factory.CreateDbContextAsync();var x=await db.AppSettings.FirstOrDefaultAsync(s=>s.CompanyId==m.CompanyId&&s.UserId==m.UserId);var now=DateTime.UtcNow;var operation=SyncOperation.Update;if(x is null){operation=SyncOperation.Create;m.Id=m.Id==Guid.Empty?Guid.NewGuid():m.Id;x=new LocalAppSettingsEntity{Id=m.Id,CompanyId=m.CompanyId,UserId=m.UserId,CreatedAtUtc=now};db.AppSettings.Add(x);}Apply(x,m,now);LocalSyncQueueWriter.Enqueue(db,"AppSettings",x.Id,x.CompanyId,operation,Payload(x),now);await db.SaveChangesAsync();m.SyncStatus=SyncStatus.PendingSync;m.UpdatedAtUtc=now;return(true,null);}
 private string? Validate(AppSettings m){if(_session.CurrentCompanyId!=m.CompanyId||_session.CurrentUserId!=m.UserId)return"You do not have access to these settings.";if(!Enum.IsDefined(m.Theme))return"Invalid theme.";if(!Regex.IsMatch(m.AccentColor??"", "^#[0-9A-Fa-f]{6}$"))return"Accent color must be a six-digit hex color.";if(!Languages.Contains(m.Language))return"Unsupported language.";if(!DateFormats.Contains(m.DateFormat))return"Unsupported date format.";if(!Currencies.TryGetValue(m.Currency,out var symbol)||symbol!=m.CurrencySymbol)return"Invalid currency or currency symbol.";if(string.IsNullOrWhiteSpace(m.TimeZone)||m.TimeZone.Length>100)return"Invalid time zone.";if(m.OfflineMode&&m.AutoSync)return"Auto-sync must be disabled while offline mode is enabled.";return null;}
 private static AppSettings Map(LocalAppSettingsEntity x)=>new(){Id=x.Id,UserId=x.UserId,CompanyId=x.CompanyId,Theme=(ThemeMode)x.Theme,AccentColor=x.AccentColor,CompactMode=x.CompactMode,Language=x.Language,DateFormat=x.DateFormat,Currency=x.Currency,CurrencySymbol=x.CurrencySymbol,TimeZone=x.TimeZone,Notifications=x.Notifications,AutoSync=x.AutoSync,OfflineMode=x.OfflineMode,RememberLogin=x.RememberLogin,LastCompanyId=x.LastCompanyId,SyncStatus=ToUi(x.SyncStatus),UpdatedAtUtc=x.UpdatedAtUtc};
 private static void Apply(LocalAppSettingsEntity x,AppSettings m,DateTime now){x.Theme=(int)m.Theme;x.AccentColor=m.AccentColor.ToUpperInvariant();x.CompactMode=m.CompactMode;x.Language=m.Language;x.DateFormat=m.DateFormat;x.Currency=m.Currency;x.CurrencySymbol=m.CurrencySymbol;x.TimeZone=m.TimeZone.Trim();x.Notifications=m.Notifications;x.AutoSync=m.AutoSync;x.OfflineMode=m.OfflineMode;x.RememberLogin=m.RememberLogin;x.LastCompanyId=m.LastCompanyId;x.SyncStatus=(int)RecordSyncStatus.Pending;x.UpdatedAtUtc=now;x.SyncError=null;}
 private static object Payload(LocalAppSettingsEntity x)=>new{x.Id,x.UserId,x.CompanyId,x.Theme,x.AccentColor,x.CompactMode,x.Language,x.DateFormat,x.Currency,x.CurrencySymbol,x.TimeZone,x.Notifications,x.AutoSync,x.OfflineMode,x.RememberLogin};private static SyncStatus ToUi(int s)=>(RecordSyncStatus)s==RecordSyncStatus.Synced?SyncStatus.Synced:(RecordSyncStatus)s==RecordSyncStatus.Failed?SyncStatus.SyncFailed:SyncStatus.PendingSync;
}

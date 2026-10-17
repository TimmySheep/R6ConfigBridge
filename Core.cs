using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Xml.Serialization;
namespace R6ConfigBridge {
 public class Field {
  public string Id,Group,Label;
  public Field(string section,string key,string group,string label){Id=section+"/"+key;Group=group;Label=label;}
 }
 public static class Catalog {
  public static readonly List<Field> Fields=new List<Field>();
  static void Add(string section,string group,string keys){foreach(string k in keys.Split(' '))if(k.Length>0)Fields.Add(new Field(section,k,group,k));}
  static Catalog(){
   Add("INPUT","鼠标与瞄准","RawInputMouseKeyboard InvertMouseAxisY MouseSensitivity MouseYawSensitivity MousePitchSensitivity MouseSensitivityMultiplierUnit MouseScroll XFactorAiming AimDownSightsMouse ADSMouseUseSpecific ADSMouseSensitivityGlobal ADSMouseSensitivity1x ADSMouseSensitivity1xHalf ADSMouseSensitivity2x ADSMouseSensitivity2xHalf ADSMouseSensitivity3x ADSMouseSensitivity4x ADSMouseSensitivity5x ADSMouseSensitivity12x ADSMouseSensitivity8x ADSMouseMultiplierUnit");
   Add("INPUT","按住／切换操作","ToggleAim ToggleLean ToggleSprint ToggleCrouch ToggleProne ToggleWalk ToggleDroneBoost ToggleGadgetDeploymentKeyboard");
   Add("INPUT","手柄","InvertAxisY Rumble YawSensitivity PitchSensitivity DeadzoneLeftStick DeadzoneRightStick DeadzoneTriggerLeft DeadzoneTriggerRight DeadzoneSwitchInputStick ControllerStickRotationCurve ToggleAimGamepad ToggleGadgetDeploymentGamepad AimDownSights ADSGamepadUseSpecific ADSGamepadSensitivityGlobal ADSGamepadSensitivity1x ADSGamepadSensitivity1xHalf ADSGamepadSensitivity2x ADSGamepadSensitivity2xHalf ADSGamepadSensitivity3x ADSGamepadSensitivity4x ADSGamepadSensitivity5x ADSGamepadSensitivity12x ADSGamepadSensitivity8x ADSGamepadMultiplierUnit AdvancedGamepadOptions GamepadHorizontalFullTiltBoostModifier GamepadVerticalFullTiltBoostModifier FullTiltBoostRampupDelay GamepadFullTiltBoostRampupTime ADSGamepadHorizontalFullTiltBoostModifier ADSGamepadVerticalFullTiltBoostModifier ADSFullTiltBoostRampupDelay ADSGamepadFullTiltBoostRampupTime OuterDeadzoneRightStick GamepadLookDampeningTime ControlSchemeIndex ControllerInputDevice");
   Add("DISPLAY","显示","Brightness FPSLimit NVReflex NVReflexIndicator Console");
   Add("DISPLAY_SETTINGS","显示","ResolutionWidth ResolutionHeight RefreshRate WindowMode AspectRatio VSync UseLetterbox DefaultFOV");
   Add("QUALITY","画质","OverallQualityLevelName");
   Add("CUSTOM_QUALITY","画质","AntiAliasing Atmospheric Geometry Lighting Shadow Sharpness Texture VFX TextureFiltering Reflection AO LensEffects DOF AdaptiveRenderScalingTargetFPS RenderScalingFactor DLSSPerfQual FSR2PerfQual FSRPerfQual TextureStreaming TextureVRAMLimit TemporalUpscalerMode Upscaler");
   Add("AUDIO","声音与语音","SubtitleType Subtitle MasterVolume MenuMusicVolume InGameMusicVolume MenuSFXVolume InGameSFXVolume VoiceVolume DynamicRangeMode MonoOutput VoiceChatEnabled VoiceChatPlaybackLevel VoiceChatCaptureThresholdV2 VoiceChatCaptureMode VoiceChatCaptureLevel Mute VoiceChatMuteAll VoiceChatTeamOnly");
   Add("GAMEPLAY","游戏与辅助功能","GameplayPingEnable");
   Add("ACCESSIBILITY","游戏与辅助功能","AccessibilityColorMode PositiveColorIndex NegativeColorIndex ObjectiveColorIndex PingColorIndex TeamColorAllyIndex TeamColorEnemyIndex StunVFXMode TinnitusSFXMode HearingFatigueAid");
   Dictionary<string,string> labels=new Dictionary<string,string>{{"MouseYawSensitivity","鼠标水平灵敏度"},{"MousePitchSensitivity","鼠标垂直灵敏度"},{"MouseSensitivityMultiplierUnit","鼠标灵敏度乘数"},{"RawInputMouseKeyboard","原始鼠标／键盘输入"},{"ADSMouseUseSpecific","启用独立倍率灵敏度"},{"ADSMouseSensitivity1x","1 倍镜灵敏度"},{"ADSMouseSensitivity2xHalf","2.5 倍镜灵敏度"},{"DefaultFOV","垂直视野 FOV"},{"FPSLimit","帧率上限（低于 30 不限帧）"},{"MasterVolume","主音量"},{"DynamicRangeMode","动态范围（0 Hi-Fi / 1 TV / 2 夜间）"},{"MouseSensitivity","MouseSensitivity（保留字段，非水平滑条）"},{"AimDownSightsMouse","AimDownSightsMouse（生效关系待核实）"}};
   foreach(Field f in Fields){string k=f.Id.Split('/')[1];if(labels.ContainsKey(k))f.Label=labels[k];}
  }
  public static bool Allowed(string id){return Fields.Exists(delegate(Field f){return f.Id==id;});}
 }
 public class Ini {
  public Dictionary<string,string> Values=new Dictionary<string,string>(StringComparer.Ordinal);
  readonly byte[] original; readonly string text; readonly bool bom;
  public Ini(byte[] bytes){
   if(bytes.Length>1024*1024)throw new Exception("配置文件过大，拒绝读取。");
   if(bytes.Length>1 && ((bytes[0]==255&&bytes[1]==254)||(bytes[0]==254&&bytes[1]==255)))throw new Exception("暂不支持 UTF-16 配置。");
   original=(byte[])bytes.Clone();bom=bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191;
   text=Encoding.GetEncoding(28591).GetString(bytes,bom?3:0,bytes.Length-(bom?3:0));
   string section="";foreach(Match m in Regex.Matches(text,@"[^\r\n]+")){
    string l=m.Value.Trim();if(l.Length==0||l.StartsWith(";")||l.StartsWith("#"))continue;
    if(l.StartsWith("[")&&l.EndsWith("]")){section=l.Substring(1,l.Length-2);continue;}
    int eq=l.IndexOf('=');if(eq<1)continue;string id=section+"/"+l.Substring(0,eq).Trim();
    if(Values.ContainsKey(id))throw new Exception("配置中有重复字段："+id);Values[id]=l.Substring(eq+1).Trim();
   }
  }
  public byte[] Patch(Ini source,IEnumerable<string> ids){
   HashSet<string> selected=new HashSet<string>(ids,StringComparer.Ordinal);
   if(selected.Count==0)throw new Exception("请先勾选要同步的项目。");
   foreach(string id in selected){if(!Catalog.Allowed(id))throw new Exception("不允许同步内部字段："+id);if(!Values.ContainsKey(id)||!source.Values.ContainsKey(id))throw new Exception("两边必须都存在该字段："+id);}
   string section="";
   string result=Regex.Replace(text,@"[^\r\n]+",delegate(Match m){
    string l=m.Value,t=l.Trim();if(t.StartsWith("[")&&t.EndsWith("]")){section=t.Substring(1,t.Length-2);return l;}
    if(t.StartsWith(";")||t.StartsWith("#"))return l;int eq=l.IndexOf('=');if(eq<0)return l;
    string id=section+"/"+l.Substring(0,eq).Trim();if(!selected.Contains(id))return l;
    string after=l.Substring(eq+1);int leading=after.Length-after.TrimStart().Length;int trailing=after.Length-after.TrimEnd().Length;
    if(leading+trailing>after.Length)trailing=0;
    return l.Substring(0,eq+1)+after.Substring(0,leading)+source.Values[id]+(trailing>0?after.Substring(after.Length-trailing):"");
   });
   byte[] body=Encoding.GetEncoding(28591).GetBytes(result);if(!bom)return body;
   byte[] output=new byte[body.Length+3];Array.Copy(original,output,3);Array.Copy(body,0,output,3,body.Length);return output;
  }
 }
 public class Snapshot {
  public string Path,Hash; public byte[] Bytes;public Ini Ini;
  public static Snapshot Load(string path){Snapshot s=new Snapshot();s.Path=System.IO.Path.GetFullPath(path);s.Bytes=File.ReadAllBytes(s.Path);s.Hash=Store.Hash(s.Bytes);s.Ini=new Ini(s.Bytes);return s;}
 }
 public class BackupInfo {public string Target; public string BeforeHash;public string AfterHash;public string CreatedUtc;}
 public static class Store {
  public static string Hash(byte[] b){using(SHA256 s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","");}
  static void Fresh(Snapshot s){if(Hash(File.ReadAllBytes(s.Path))!=s.Hash)throw new Exception("文件在预览后发生变化，请刷新后重新选择。");}
  public static bool GameRunning(){foreach(Process p in Process.GetProcesses()){using(p){string n=p.ProcessName;if(n.IndexOf("Rainbow",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("Siege",StringComparison.OrdinalIgnoreCase)>=0)return true;}}return false;}
  static void Guard(Func<bool> running){if(running())throw new Exception("检测到彩虹六号仍在运行。请正常退出两服后再应用／恢复，工具不会替你关闭游戏。");}
  public static string Apply(Snapshot source,Snapshot target,string[] ids,string backups,Func<bool> running){
   Guard(running);if(string.Equals(source.Path,target.Path,StringComparison.OrdinalIgnoreCase))throw new Exception("来源与目标不能是同一个文件。");Fresh(source);Fresh(target);
   if(source.Ini.Values.ContainsKey("GENERAL/Version")&&target.Ini.Values.ContainsKey("GENERAL/Version")&&source.Ini.Values["GENERAL/Version"]!=target.Ini.Values["GENERAL/Version"])throw new Exception("配置版本不同，暂不支持跨版本写入。");
   byte[] next=target.Ini.Patch(source.Ini,ids);if(Hash(next)==target.Hash)throw new Exception("勾选项目没有差异，无需写入。");
   return Commit(target,next,backups,running);
  }
  static string Commit(Snapshot target,byte[] next,string backups,Func<bool> running){
   Guard(running);Fresh(target);Directory.CreateDirectory(backups);string token=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-"+Guid.NewGuid().ToString("N");
   string backup=System.IO.Path.Combine(backups,token+".ini");File.WriteAllBytes(backup,target.Bytes);
   if(Hash(File.ReadAllBytes(backup))!=target.Hash)throw new Exception("备份校验失败，未写入。");
   BackupInfo info=new BackupInfo{Target=target.Path,BeforeHash=target.Hash,AfterHash=Hash(next),CreatedUtc=DateTime.UtcNow.ToString("o")};
   using(FileStream f=new FileStream(backup+".xml",FileMode.CreateNew))new XmlSerializer(typeof(BackupInfo)).Serialize(f,info);
   string staging=target.Path+".r6bridge-"+token+".stage";
   using(FileStream f=new FileStream(staging,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(next,0,next.Length);f.Flush(true);}
   Guard(running);Fresh(target);File.Replace(staging,target.Path,null);
   if(Hash(File.ReadAllBytes(target.Path))!=info.AfterHash)throw new Exception("写后校验失败。已保留备份："+backup);
   return backup;
  }
  public static BackupInfo ReadBackup(string backup){using(FileStream f=File.OpenRead(backup+".xml"))return (BackupInfo)new XmlSerializer(typeof(BackupInfo)).Deserialize(f);}
  public static void Restore(string backup,string targetPath,string backups,Func<bool> running){
   Guard(running);BackupInfo info=ReadBackup(backup);if(!string.Equals(System.IO.Path.GetFullPath(targetPath),info.Target,StringComparison.OrdinalIgnoreCase))throw new Exception("备份不属于选定的目标配置。");
   byte[] bytes=File.ReadAllBytes(backup);if(Hash(bytes)!=info.BeforeHash)throw new Exception("备份内容校验失败。");
   Snapshot target=Snapshot.Load(targetPath);if(target.Hash!=info.AfterHash)throw new Exception("目标在此次迁移后又发生变化。为保护新设置，拒绝直接恢复旧备份。");
   Commit(target,bytes,backups,running);
  }
 }
}

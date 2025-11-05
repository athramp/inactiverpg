namespace Agnostic {
  public readonly struct CastSkillCmd { public readonly int CasterId; public readonly int SkillId; public CastSkillCmd(int c,int s){CasterId=c;SkillId=s;} }
  public interface ICommandSink { void Enqueue(CastSkillCmd cmd); }
}

namespace Game.Core.Intake
{
    public interface IIntakePost
    {
        IntakeOutcome Post(string url, string json);
    }
}

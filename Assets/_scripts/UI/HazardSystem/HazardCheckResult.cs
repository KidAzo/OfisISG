using System.Collections.Generic;

public class HazardCheckResult : ICheckResultProvider
{
    private List<ICheckable> _foundedChecks = new();
    private List<ICheckable> _missedChecks = new();

    public List<ICheckable> foundedChecks => _foundedChecks;
    public List<ICheckable> missedChecks => _missedChecks;

	public int TotalHazards => foundedChecks.Count + missedChecks.Count;

	public float FoundRatio => TotalHazards == 0
		? 0f
		: (float)foundedChecks.Count / TotalHazards;
}

public interface ICheckable
{
	string TaskName { get; }
}

public interface ICheckResultProvider
{
	List<ICheckable> foundedChecks { get; }
	List<ICheckable> missedChecks { get; }
	float FoundRatio { get; }
	int TotalHazards { get; }
}


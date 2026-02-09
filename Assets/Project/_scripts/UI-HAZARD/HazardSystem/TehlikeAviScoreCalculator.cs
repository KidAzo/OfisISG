using UnityEngine;

public static class HazardScoreCalculator
{
	public static void GetGrade(int totalScore, out string letter, out string description)
	{
		if (totalScore >= 90)
		{
			letter = "A";
			description = "Cok iyi";
		}
		else if (totalScore >= 75)
		{
			letter = "B";
			description = "Iyi";
		}
		else if (totalScore >= 60)
		{
			letter = "C";
			description = "Orta";
		}
		else
		{
			letter = "D";
			description = "Gelistirilmeli";
		}
	}
}

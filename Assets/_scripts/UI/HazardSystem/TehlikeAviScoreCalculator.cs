using UnityEngine;

public static class TehlikeAviScoreCalculator
{
	public static void GetGrade(int totalScore, out string letter, out string description)
	{
		if (totalScore >= 90)
		{
			letter = "A";
			description = "�ok �yi";
		}
		else if (totalScore >= 75)
		{
			letter = "B";
			description = "�yi";
		}
		else if (totalScore >= 60)
		{
			letter = "C";
			description = "Orta";
		}
		else
		{
			letter = "D";
			description = "Geli�tirilmeli";
		}
	}
}

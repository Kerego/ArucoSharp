using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Windows.UI.Popups;

namespace CrosswordSolver
{
    public class Solver
    {
		#region Fields
		List<Rebus> arr = new List<Rebus>();
		IList<string> _words;

		public enum Orientation
		{
			Horizontal,
			Vertical
		}

		public struct Rebus
		{
			public int xorigin, yorigin;
			public Orientation orient;
			public int length;
			public int Id;
			public List<Intersect> intersects;
		}
		public struct Intersect
		{
			public int xIntersect, yIntersect;
			public int IntersectId;
		}

		public struct Restriction
		{
			public int XRestriction;
			public int YRestriction;
			public char Value;
		}

		public struct StackElement
		{
			public int Order;
			public List<Restriction> Restrictions;
			public int wordIndex;
		}

		#endregion

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="Words">All the words found in the file.</param>
		public Solver(IList<string> Words)
		{
			this._words = Words;
			char[,] x = new char[3, 3];
			x[0, 0] = '1';
		}

		/// <summary>
		/// Grouped found word in int array.
		/// </summary>
		public List<IGrouping<int, Rebus>> Groups
		{
			get
			{
				return arr.GroupBy(a => a.length).ToList();
			}
		}
		/// <summary>
		/// Convert a bidimensional int array into a <see cref="List{<see cref="Rebus"/>}"/>
		/// </summary>
		/// <param name="matrix"></param>
		/// <param name="xmax"></param>
		/// <param name="ymax"></param>
		public bool Test(int[,] matrix, int xmax, int ymax)
		{


			int i = 1, x, y;
			Rebus temp;



			//horizontal

			for (x = 0; x < xmax; x++)
			{
				y = 0;
				while (y < ymax - 1)
				{
					if (matrix[x, y] != 0)
					{
						if (matrix[x, y + 1] != 0)
						{
							temp = new Rebus { length = 0, xorigin = x, yorigin = y, Id = i, orient = Orientation.Horizontal, intersects = new List<Intersect>() };
							do
							{
								temp.length++;
								matrix[x, y] = i;
								y++;
							} while (y < ymax && matrix[x, y] != 0);
							arr.Add(temp);
							i++;
						}
						else
							y++;
					}
					else y++;
				}
			}

			//vertical
			for (y = 0; y < ymax; y++)
			{
				x = 0;
				while (x < xmax - 1)
				{
					if (matrix[x, y] != 0)
					{
						if (matrix[x + 1, y] != 0)
						{
							temp = new Rebus { length = 0, xorigin = x, yorigin = y, Id = i, orient = Orientation.Vertical, intersects = new List<Intersect>() };
							do
							{
								temp.length++;
								if (matrix[x, y] != -1)
								{
									arr[matrix[x, y] - 1].intersects.Add(new Intersect() { xIntersect = x, yIntersect = y, IntersectId = i });
									temp.intersects.Add(new Intersect() { xIntersect = x, yIntersect = y, IntersectId = matrix[x, y] });
								}
								matrix[x, y] = i;
								x++;
							} while (x < xmax && matrix[x, y] != 0);
							arr.Add(temp);
							i++;
						}
						else
							x++;
					}
					else
						x++;
				}
			}
			return arr.Any();
		}

		/// <summary>
		/// Lungimea maxima a cuvintelor posibile.
		/// </summary>
		public int Max
		{
			get
			{
				int max = arr[0].length;
				for (int i = 0; i < arr.Count; i++)
				{
					if (arr[i].length > max)
					{
						max = arr[i].length;
					}
				}
				return max;
			}
		}

		public StackElement? CreateStackElement(IGrouping<int, string> words, ref char[,] matrix, int wordIndex, int elementI, List<Restriction> restrictions)
		{
			int wCount = wordIndex;
			bool flag = true;
			if (restrictions.Count == 0)
			{
				var word = words.ElementAt(wCount++).ToCharArray();
				WriteWord(matrix, elementI, word);
			}
			else
			{
				int i = -1;
				foreach (var word in words)
				{
					i++;
					if (i < wordIndex)
						continue;
					flag = true;
					var wordArray = word.ToCharArray();
					foreach (var restriction in restrictions)
					{
						if (arr[elementI].orient == Orientation.Horizontal)
						{
							if (wordArray[restriction.YRestriction - arr[elementI].yorigin] != restriction.Value)
							{
								flag = false;
								break;
							}
						}
						else
						{
							if (wordArray[restriction.XRestriction - arr[elementI].xorigin] != restriction.Value)
							{
								flag = false;
								break;
							}
						}
					}
					if (flag)
					{
						WriteWord(matrix, elementI, wordArray);
						wCount++;
						break;
					}

					wCount++;
				}
				if (!flag)
					return null;
			}
			return new StackElement { Order = elementI, wordIndex = wCount, Restrictions = restrictions };
		}

		private void WriteWord(char[,] matrix, int elementI, char[] word)
		{
			for (int i = 0, len = arr[elementI].length; i < len; i++)
			{
				if (arr[elementI].orient == Orientation.Horizontal)
					matrix[arr[elementI].xorigin, arr[elementI].yorigin + i] = word[i];
				else
					matrix[arr[elementI].xorigin + i, arr[elementI].yorigin] = word[i];
			}
		}

		private void ClearWord(char[,] matrix, int elementI)
		{
			for (int i = 0, len = arr[elementI].length; i < len; i++)
			{
				if (arr[elementI].orient == Orientation.Horizontal)
					matrix[arr[elementI].xorigin, arr[elementI].yorigin + i] = '\0';
				else
					matrix[arr[elementI].xorigin + i, arr[elementI].yorigin] = '\0';
			}
		}


		/// <summary>
		/// TestFunc
		/// </summary>
		/// <param name="xmax"></param>
		/// <param name="ymax"></param>
		/// <returns></returns>
		public char[,] FirstTest(int xmax, int ymax)
		{

			char[,] result = new char[xmax, ymax];
			IList<string> PWords = GetPossibleWords(Max);
			var groups = PWords.GroupBy(word => word.Length).ToList();

			int wordIndex = 0;
			Stack<StackElement> stack = new Stack<StackElement>();

			arr = arr.OrderByDescending(x => x.intersects.Count).ToList();

			int i = 0;
			while (i < arr.Count)
			{
				var restrictions = new List<Restriction>();
				foreach (var item in arr[i].intersects)
				{
					if (result[item.xIntersect, item.yIntersect] != '\0')
					{
						restrictions.Add(new Restriction
						{
							XRestriction = item.xIntersect,
							YRestriction = item.yIntersect,
							Value = result[item.xIntersect, item.yIntersect]
						});
					}
				}
				var test = CreateStackElement(groups.Single(group => group.Key == arr[i].length), ref result, wordIndex, i, restrictions);
				StackElement element;
				if (test.HasValue)
				{
					element = test.Value;
					stack.Push(element);
					wordIndex = 0;
					i++;
				}
				else
				{
					element = stack.Pop();
					ClearWord(result, element.Order);
					foreach (var restriction in element.Restrictions)
					{
						result[restriction.XRestriction, restriction.YRestriction] = restriction.Value;
					}
					i -= 1;
					wordIndex = element.wordIndex++;
				}


				//foreach(var w in groups[arr[i].length])
				//{
				//    //if no contradiction;
				//    foreach(var intersection in arr[i].intersects)
				//    {
				//        if(intersection.)
				//    }
				//    if(arr[i].intersects[])
				//    arr[i].Value = w;
				//}

				//switch (arr[i].orient)
				//{
				//    case Orientation.Horizontal:
				//        foreach (string item in PWords)
				//        {
				//            if (item.Length == arr[i].length)
				//            {
				//                for (int j = 0; j < item.Length; j++)
				//                {
				//                    result[arr[i].xorigin, arr[i].yorigin + j] = item[j];
				//                }
				//            }
				//        }
				//        break;
				//    case Orientation.Vertical:
				//        foreach (string item in PWords)
				//        {
				//            if (item.Length == arr[i].length)
				//            {
				//                for (int j = 0; j < item.Length; j++)
				//                {
				//                    result[arr[i].xorigin+j, arr[i].yorigin] = item[j];
				//                }
				//            }
				//        }
				//        break;
				//    default:
				//        break;
				//}
			}

			var s = string.Empty;
			for (int x = 0; x < result.GetLength(0); x++)
			{
				for (int j = 0; j < result.GetLength(1); j++)
				{
					s += result[x, j] == '\0' ? ' ' : result[x, j];
				}
				s += Environment.NewLine;
			}
			Debug.WriteLine(s);
			return result;
		}

		/// <summary>
		/// Returneaza cuvintele posibile de cuvinte cu numarul lungimii maximale a cuvintelor indicat.
		/// </summary>
		/// <param name="max">Lungimea maximala a cuvintelor.</param>
		/// <returns></returns>
		public IList<string> GetPossibleWords(int max)
		{
			return _words.Where(x => x.Length <= max).ToList();
		}

		/// <summary>
		/// Gaseste cuvantul potrivit pentru intersectarea cu cuvintul introdus. 
		/// </summary>
		/// <param name="firstword">Cuvintul deja prezent in rebus.</param>
		/// <param name="intrsctpos">Index zero-based, al caracterului unde cuvintele se intersecteaza.</param>
		/// <param name="requiredlength">Marimea cuvintului cautat care coincide cu spatiile in rebus.</param>
		/// <param name="lengthbeforeint">Lungimea cuvintului cautat pina la intersectarea lor.</param>
		/// <returns></returns>
		public IList<string> FindWordForIntersect(string firstword, int intrsctpos, int requiredlength, int lengthbeforeint)
		{
			IList<string> foundwords = new List<string>();
			foreach (string item in _words)
			{
				if (item.Length == requiredlength)
				{
					char intrsctchar = firstword[intrsctpos];
					if (item.Contains(intrsctchar))
					{
						if (item.IndexOf(intrsctchar) - 0 == lengthbeforeint)
						{
							foundwords.Add(item);
						}
					}
				}
			}
			return foundwords;
		}
	}
}

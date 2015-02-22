using System;
using System.Collections.Generic;

namespace SeasonalWardrobe
{
	public enum AllowanceState
	{
		AllowAll,
		AllowHat,
		AllowWrap,
		AllowNone
	}

	public enum Command
	{
		AddHat,
		RemoveHat,
		AddWrap,
		RemoveWrap
	}

	public class Process
	{
		class StateTransition
		{
			readonly AllowanceState CurrentState;
			readonly Command Command;

			public StateTransition(AllowanceState currentState, Command command)
			{
				CurrentState = currentState;
				Command = command;
			}

			public override int GetHashCode()
			{
				return 17 + 31 * CurrentState.GetHashCode() + 31 * Command.GetHashCode();
			}

			public override bool Equals(object obj)
			{
				StateTransition other = obj as StateTransition;
				return other != null && this.CurrentState == other.CurrentState && this.Command == other.Command;
			}
		}

		Dictionary<StateTransition, AllowanceState> transitions;
		public AllowanceState CurrentState { get; private set; }

		public Process()
		{
			CurrentState = AllowanceState.AllowAll;
			transitions = new Dictionary<StateTransition, AllowanceState>
			{
				{ new StateTransition(AllowanceState.AllowAll, Command.AddHat), AllowanceState.AllowWrap },
				{ new StateTransition(AllowanceState.AllowAll, Command.AddWrap), AllowanceState.AllowHat },
				{ new StateTransition(AllowanceState.AllowHat, Command.AddHat), AllowanceState.AllowNone },
				{ new StateTransition(AllowanceState.AllowHat, Command.RemoveWrap), AllowanceState.AllowAll },
				{ new StateTransition(AllowanceState.AllowWrap, Command.AddWrap), AllowanceState.AllowNone },
				{ new StateTransition(AllowanceState.AllowWrap, Command.RemoveHat), AllowanceState.AllowAll },
				{ new StateTransition(AllowanceState.AllowNone, Command.RemoveHat), AllowanceState.AllowHat },
				{ new StateTransition(AllowanceState.AllowNone, Command.RemoveWrap), AllowanceState.AllowWrap }
			};
		}

		public AllowanceState GetNext(Command command)
		{
			StateTransition transition = new StateTransition(CurrentState, command);
			AllowanceState nextState;
			if (!transitions.TryGetValue(transition, out nextState))
				throw new Exception("Invalid transition: " + CurrentState + " -> " + command);
			return nextState;
		}

		public AllowanceState MoveNext(Command command)
		{
			CurrentState = GetNext(command);
			return CurrentState;
		}
	}


//	public class Program
//	{
//		static void Main(string[] args)
//		{
//			Process p = new Process();
//			Console.WriteLine("Current State = " + p.CurrentState);
//			Console.WriteLine("Command.Begin: Current State = " + p.MoveNext(Command.Begin));
//			Console.WriteLine("Command.Pause: Current State = " + p.MoveNext(Command.Pause));
//			Console.WriteLine("Command.End: Current State = " + p.MoveNext(Command.End));
//			Console.WriteLine("Command.Exit: Current State = " + p.MoveNext(Command.Exit));
//			Console.ReadLine();
//		}
//	}
}
/// <summary>
/// Nifty state machine copied from a StackExchange post on C# FSMs.
/// </summary>

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

	public class FSM_Process
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

		public FSM_Process()
		{
			CurrentState = AllowanceState.AllowAll;
			transitions = new Dictionary<StateTransition, AllowanceState>
			{
				// The normal state transistions
				{ new StateTransition(AllowanceState.AllowAll, Command.AddHat), AllowanceState.AllowWrap },
				{ new StateTransition(AllowanceState.AllowAll, Command.AddWrap), AllowanceState.AllowHat },
				{ new StateTransition(AllowanceState.AllowHat, Command.AddHat), AllowanceState.AllowNone },
				{ new StateTransition(AllowanceState.AllowHat, Command.RemoveWrap), AllowanceState.AllowAll },
				{ new StateTransition(AllowanceState.AllowWrap, Command.AddWrap), AllowanceState.AllowNone },
				{ new StateTransition(AllowanceState.AllowWrap, Command.RemoveHat), AllowanceState.AllowAll },
				{ new StateTransition(AllowanceState.AllowNone, Command.RemoveHat), AllowanceState.AllowHat },
				{ new StateTransition(AllowanceState.AllowNone, Command.RemoveWrap), AllowanceState.AllowWrap },

				// These are the odd transitions, like what happens when seasons change but stored clothing
				// was never removed from the last season.
				{ new StateTransition(AllowanceState.AllowAll, Command.RemoveHat), AllowanceState.AllowHat },
				{ new StateTransition(AllowanceState.AllowAll, Command.RemoveWrap), AllowanceState.AllowWrap },
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
}
﻿using Celeste;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace TAS {
	public class InputController {
		private List<InputRecord> inputs = new List<InputRecord>();
		private int currentFrame, inputIndex, frameToNext;
		private string filePath;
		public InputController(string filePath) {
			this.filePath = filePath;
		}

		public bool CanPlayback { get { return inputIndex < inputs.Count; } }
		public bool HasFastForward { get; set; }
		public int CurrentFrame { get { return currentFrame; } }
		public int CurrentInputFrame { get { return currentFrame - frameToNext + Current.Frames; } }
		public InputRecord Current { get; set; }
		public InputRecord Previous {
			get {
				if (frameToNext != 0 && inputIndex - 1 >= 0 && inputs.Count > 0) {
					return inputs[inputIndex - 1];
				}
				return null;
			}
		}
		public InputRecord Next {
			get {
				if (frameToNext != 0 && inputIndex + 1 < inputs.Count) {
					return inputs[inputIndex + 1];
				}
				return null;
			}
		}
		public bool HasInput(Actions action) {
			InputRecord input = Current;
			return input.HasActions(action);
		}
		public bool HasInputPressed(Actions action) {
			InputRecord input = Current;

			return input.HasActions(action) && CurrentInputFrame == 1;
		}
		public bool HasInputReleased(Actions action) {
			InputRecord current = Current;
			InputRecord previous = Previous;

			return !current.HasActions(action) && previous != null && previous.HasActions(action) && CurrentInputFrame == 1;
		}
		public override string ToString() {
			if (frameToNext == 0 && Current != null) {
				return Current.ToString() + "(" + currentFrame.ToString() + ")";
			} else if (inputIndex < inputs.Count && Current != null) {
				int inputFrames = Current.Frames;
				int startFrame = frameToNext - inputFrames;
				return Current.ToString() + "(" + (currentFrame - startFrame).ToString() + " / " + inputFrames + " : " + currentFrame + ")";
			}
			return string.Empty;
		}
		public string NextInput() {
			if (frameToNext != 0 && inputIndex + 1 < inputs.Count) {
				return inputs[inputIndex + 1].ToString();
			}
			return string.Empty;
		}
		public void InitializePlayback() {
			int trycount = 5;
			while (!ReadFile() && trycount >= 0) {
				System.Threading.Thread.Sleep(50);
				trycount--;
			}

			currentFrame = 0;
			inputIndex = 0;
			if (inputs.Count > 0) {
				Current = inputs[0];
				frameToNext = Current.Frames;
			} else {
				Current = new InputRecord();
				frameToNext = 1;
			}
		}
		public void ReloadPlayback() {
			int playedBackFrames = currentFrame;
			InitializePlayback();
			currentFrame = playedBackFrames;

			while (currentFrame >= frameToNext) {
				if (inputIndex + 1 >= inputs.Count) {
					inputIndex++;
					return;
				}
				if (Current.FastForward) {
					HasFastForward = false;
				}
				Current = inputs[++inputIndex];
				frameToNext += Current.Frames;
			}
		}
		public void InitializeRecording() {
			currentFrame = 0;
			inputIndex = 0;
			Current = new InputRecord();
			frameToNext = 0;
			inputs.Clear();
		}
		public void PlaybackPlayer() {
			if (inputIndex < inputs.Count && !Manager.IsLoading()) {
				if (currentFrame >= frameToNext) {
					if (inputIndex + 1 >= inputs.Count) {
						inputIndex++;
						return;
					}
					if (Current.FastForward) {
						HasFastForward = false;
					}
					Current = inputs[++inputIndex];
					frameToNext += Current.Frames;
				}

				currentFrame++;
			}
			SetInputs();
		}
		private void SetInputs() {
			GamePadDPad pad;
			GamePadThumbSticks sticks;
			if (Current.HasActions(Actions.Feather)) {
				pad = new GamePadDPad(ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
				sticks = new GamePadThumbSticks(new Vector2(Current.GetX(), Current.GetY()), new Vector2(0, 0));
			} else {
				pad = new GamePadDPad(
					Current.HasActions(Actions.Up) ? ButtonState.Pressed : ButtonState.Released,
					Current.HasActions(Actions.Down) ? ButtonState.Pressed : ButtonState.Released,
					Current.HasActions(Actions.Left) ? ButtonState.Pressed : ButtonState.Released,
					Current.HasActions(Actions.Right) ? ButtonState.Pressed : ButtonState.Released
				);
				sticks = new GamePadThumbSticks(new Vector2(0, 0), new Vector2(0, 0));
			}
			GamePadState state = new GamePadState(
				sticks,
				new GamePadTriggers(Current.HasActions(Actions.Journal) ? 1f : 0f, 0),
				new GamePadButtons(
					(Current.HasActions(Actions.Jump) ? Buttons.A : (Buttons)0)
					| (Current.HasActions(Actions.Dash) ? Buttons.B : (Buttons)0)
					| (Current.HasActions(Actions.Grab) ? Buttons.RightShoulder : (Buttons)0)
					| (Current.HasActions(Actions.Start) ? Buttons.Start : (Buttons)0)
					| (Current.HasActions(Actions.Restart) ? Buttons.LeftShoulder : (Buttons)0)
				),
				pad
			);

			for (int i = 0; i < 4; i++) {
				MInput.GamePads[i].Update();
				if (MInput.GamePads[i].Attached) {
					MInput.GamePads[i].CurrentState = state;
				}
			}
			MInput.UpdateVirtualInputs();
		}
		public void RecordPlayer() {
			InputRecord input = new InputRecord() { Line = inputIndex + 1, Frames = currentFrame };
			GetCurrentInputs(input);

			if (currentFrame == 0 && input == Current) {
				return;
			} else if (input != Current && !Manager.IsLoading()) {
				Current.Frames = currentFrame - Current.Frames;
				inputIndex++;
				if (Current.Frames != 0) {
					inputs.Add(Current);
				}
				Current = input;
			}
			currentFrame++;
		}
		private static void GetCurrentInputs(InputRecord record) {
			if (Input.Jump.Check) { record.Actions |= Actions.Jump; }
			if (Input.Dash.Check) { record.Actions |= Actions.Dash; }
			if (Input.Grab.Check) { record.Actions |= Actions.Grab; }
			if (Input.MenuJournal.Check) { record.Actions |= Actions.Journal; }
			if (Input.Pause.Check) { record.Actions |= Actions.Start; }
			if (Input.Aim.Value.X < -0.5f) { record.Actions |= Actions.Left; }
			if (Input.Aim.Value.X > 0.5f) { record.Actions |= Actions.Right; }
			if (Input.Aim.Value.Y < -0.5f) { record.Actions |= Actions.Up; }
			if (Input.Aim.Value.Y > 0.5) { record.Actions |= Actions.Down; }
		}
		public void WriteInputs() {
			using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
				for (int i = 0; i < inputs.Count; i++) {
					InputRecord record = inputs[i];
					byte[] data = Encoding.ASCII.GetBytes(record.ToString() + "\r\n");
					fs.Write(data, 0, data.Length);
				}
				fs.Close();
			}
		}
		private bool ReadFile() {
			try {
				inputs.Clear();
				HasFastForward = false;
				if (!File.Exists(filePath)) { return false; }

				int lines = 0;
				using (StreamReader sr = new StreamReader(filePath)) {
					while (!sr.EndOfStream) {
						string line = sr.ReadLine();

						InputRecord input = new InputRecord(++lines, line);
						if (input.Frames != 0) {
							inputs.Add(input);
						} else if (input.FastForward) {
							HasFastForward = true;
							if (inputs.Count > 0) {
								inputs[inputs.Count - 1].FastForward = true;
							}
						}
					}
				}
				return true;
			} catch {
				return false;
			}
		}
	}
}
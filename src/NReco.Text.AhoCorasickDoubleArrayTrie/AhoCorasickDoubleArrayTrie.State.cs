/*
 *  Copyright 2017 Vitalii Fedorchenko
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the Apache License version 2.
 *
 *  This C# implementation is a port of hankcs's https://github.com/hankcs/AhoCorasickDoubleArrayTrie (java)
 *  that licensed under the Apache 2.0 License (see http://www.apache.org/licenses/LICENSE-2.0).
 *
 *  Unless required by applicable law or agreed to in writing, software distributed on an
 *  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 */

namespace NReco.Text;

using System;
using System.Collections.Generic;
using System.Text;

public class AhoCorasickDoubleArrayTrieState {
	private ISet<int>? emits;

	public AhoCorasickDoubleArrayTrieState()
	 : this(0) {
	}

	public AhoCorasickDoubleArrayTrieState(int depth) {
		this.Depth = depth;
	}

	public int Depth { get; }

	private int largestValueId = int.MinValue;

	public void AddEmit(int keyword) {
		if (this.emits == null) {
			this.emits = new HashSet<int>();
		}

		if (keyword > this.largestValueId) {
			this.largestValueId = keyword;
		}

		this.emits.Add(keyword);
	}

	public int LargestValueId =>
		this.emits == null || this.emits.Count == 0
			? int.MinValue // ?? null
			: this.largestValueId;

	public void AddEmit(IEnumerable<int> emits) {
		if (emits == null) {
			throw new ArgumentNullException(nameof(emits));
		}

		foreach (int emit in emits) {
			this.AddEmit(emit);
		}
	}

	public ICollection<int> Emit =>
		this.emits ?? (ICollection<int>)Array.Empty<int>();

	public bool IsAcceptable =>
		this.Depth > 0 && this.emits != null;

	public AhoCorasickDoubleArrayTrieState? Failure { get; private set; }

	public void SetFailure(AhoCorasickDoubleArrayTrieState failState, IList<int> fail) {
		if (fail == null) {
			throw new ArgumentNullException(nameof(fail));
		}

		this.Failure = failState ?? throw new ArgumentNullException(nameof(failState));
		fail[this.Index] = failState.Index;
	}

	private AhoCorasickDoubleArrayTrieState? NextState(char character, bool ignoreRootState) {
		this.Success.TryGetValue(character, out AhoCorasickDoubleArrayTrieState? nextState);
		if (!ignoreRootState && nextState == null && this.Depth == 0) {
			nextState = this;
		}

		return nextState;
	}

	public AhoCorasickDoubleArrayTrieState? NextState(char character) =>
		this.NextState(character, false);

	public AhoCorasickDoubleArrayTrieState? NextStateIgnoreRootState(char character) =>
		this.NextState(character, true);

	public AhoCorasickDoubleArrayTrieState AddState(char character) {
		AhoCorasickDoubleArrayTrieState? nextState = this.NextStateIgnoreRootState(character);
		if (nextState == null) {
			nextState = new AhoCorasickDoubleArrayTrieState(this.Depth + 1);
			this.Success[character] = nextState;
		}
		return nextState;
	}

	public IEnumerable<AhoCorasickDoubleArrayTrieState> States =>
		this.Success.Values;

	public IEnumerable<char> Transitions =>
		this.Success.Keys;

	public override string ToString() {
		var sb = new StringBuilder("State{");
		sb.Append("depth=").Append(this.Depth)
			.Append(", ID=").Append(this.Index)
			.Append(", emits=").Append(this.emits)
			.Append(", success=").Append(this.Success.Keys)
			.Append(", failureID=").Append(this.Failure == null ? "-1" : this.Failure.Index)
			.Append(", failure=").Append(this.Failure)
			.Append('}');
		return sb.ToString();
	}

	public IDictionary<char, AhoCorasickDoubleArrayTrieState> Success { get; } = new SortedDictionary<char, AhoCorasickDoubleArrayTrieState>();

	public int Index { get; set; }
}

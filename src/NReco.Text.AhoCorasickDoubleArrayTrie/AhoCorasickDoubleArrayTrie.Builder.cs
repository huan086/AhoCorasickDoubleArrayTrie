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

/// <summary>
/// A builder to build the AhoCorasickDoubleArrayTrie
/// </summary>
/// <typeparam name="TValue">The type of value returned when search strings match.</typeparam>
public class AhoCorasickDoubleArrayTrieBuilder<TValue> {
	private readonly bool ignoreCase;

	/// <summary>
	/// Outer value array
	/// </summary>
	private readonly List<TValue> values = new();

	/// <summary>
	/// The length of every key.
	/// </summary>
	private readonly List<int> keyLengths = new();

	/// <summary>
	/// Check array of the Double Array Trie structure
	/// </summary>
	private int[] check = Array.Empty<int>();

	/// <summary>
	/// Base array of the Double Array Trie structure
	/// </summary>
	private int[] @base = Array.Empty<int>();

	/// <summary>
	/// Fail table of the Aho Corasick automata
	/// </summary>
	private IList<int> fail = Array.Empty<int>();

	/// <summary>
	/// Output table of the Aho Corasick automata
	/// </summary>
	private IList<IList<int>?> output = Array.Empty<IList<int>?>();

	/// <summary>
	/// The root state of trie.
	/// </summary>
	private AhoCorasickDoubleArrayTrieState? rootState = new();

	/// <summary>
	/// Whether the position has been used
	/// </summary>
	private bool[] used = Array.Empty<bool>();

	/// <summary>
	/// The allocSize of the dynamic array
	/// </summary>
	private int allocSize;

	/// <summary>
	/// A parameter controls the memory growth speed of the dynamic array.
	/// </summary>
	private int progress;

	/// <summary>
	/// The next position to check unused memory
	/// </summary>
	private int nextCheckPos;

	/// <summary>
	/// The size of base and check array
	/// </summary>
	private int size;

	public AhoCorasickDoubleArrayTrieBuilder(bool ignoreCase = false) =>
		this.ignoreCase = ignoreCase;

	public AhoCorasickDoubleArrayTrie<TValue> Build() {
		this.BuildDoubleArrayTrie();
		this.used = Array.Empty<bool>();
		this.ConstructFailureStates();
		this.rootState = null;
		this.LoseWeight();

		return new AhoCorasickDoubleArrayTrie<TValue>(this.ignoreCase, this.keyLengths.ToArray(), this.@base, this.check, this.fail, this.output, this.values.ToArray());
	}

	/// <summary>
	/// fetch siblings of a parent node
	/// </summary>
	/// <param name="parent">parent node</param>
	/// <param name="siblings">siblings parent node's child nodes, i . e . the siblings</param>
	/// <returns>the amount of the siblings</returns>
	private static int Fetch(AhoCorasickDoubleArrayTrieState parent, IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>> siblings) {
		if (parent.IsAcceptable) {
			AhoCorasickDoubleArrayTrieState fakeNode = new(-(parent.Depth + 1));
			fakeNode.AddEmit(parent.LargestValueId);
			siblings.Add(new KeyValuePair<int, AhoCorasickDoubleArrayTrieState>(0, fakeNode));
		}

		foreach (var entry in parent.Success) {
			siblings.Add(new KeyValuePair<int, AhoCorasickDoubleArrayTrieState>(entry.Key + 1, entry.Value));
		}

		return siblings.Count;
	}

	/// <summary>
	/// Add a keyword
	/// </summary>
	/// <param name="keyword">The string to search.</param>
	/// <param name="value">The value to return when keyword matches.</param>
	/// <exception cref="ArgumentNullException"><paramref name="keyword"/> is <c>null</c>.</exception>
	/// <exception cref="InvalidOperationException"><see cref="Build" /> has been called previously.</exception>
	public void AddKeyword(string keyword, TValue value) {
		if (keyword == null) {
			throw new ArgumentNullException(nameof(keyword));
		}

		if (this.ignoreCase) {
			keyword = keyword.ToUpperInvariant();
		}

		int index = this.keyLengths.Count;
		AhoCorasickDoubleArrayTrieState? currentState = this.rootState;
		if (currentState == null) {
			throw new InvalidOperationException("Cannot add keyword after Build");
		}

		for (int i = 0; i < keyword.Length; i++) {
			char character = keyword[i];
			currentState = currentState.AddState(character);
		}

		currentState.AddEmit(index);

		this.keyLengths.Add(keyword.Length);
		this.values.Add(value);
	}

	/// <summary>
	/// Add a collection of keywords
	/// </summary>
	/// <param name="keywordSet">Pairs of string to search and the value to return when it matches.</param>
	/// <exception cref="ArgumentNullException"><paramref name="keywordSet"/> is <c>null</c>.</exception>
	/// <exception cref="InvalidOperationException"><see cref="Build" /> has been called previously.</exception>
	public void AddAllKeyword(IEnumerable<KeyValuePair<string, TValue>> keywordSet) {
		if (keywordSet == null) {
			throw new ArgumentNullException(nameof(keywordSet));
		}

		// if collection size is known, let's add it more efficiently
		if (keywordSet is ICollection<KeyValuePair<string, TValue>> keywordCollection) {
			this.AddAllKeyword(keywordCollection);
			return;
		}

		foreach (var entry in keywordSet) {
			this.AddKeyword(entry.Key, entry.Value);
		}
	}

	/// <summary>
	/// Add a collection of keywords
	/// </summary>
	/// <param name="keywordSet">Pairs of string to search and the value to return when it matches.</param>
	/// <exception cref="ArgumentNullException"><paramref name="keywordSet"/> is <c>null</c>.</exception>
	/// <exception cref="InvalidOperationException"><see cref="Build" /> has been called previously.</exception>
	public void AddAllKeyword(ICollection<KeyValuePair<string, TValue>> keywordSet) {
		if (keywordSet == null) {
			throw new ArgumentNullException(nameof(keywordSet));
		}

		int ensureCapacity = keywordSet.Count + this.keyLengths.Count;
		if (this.keyLengths.Capacity < ensureCapacity) {
			int newCapacity = Math.Max(this.keyLengths.Capacity * 2, ensureCapacity);
			this.keyLengths.Capacity = newCapacity;
			this.values.Capacity = newCapacity;
		}

		foreach (var entry in keywordSet) {
			this.AddKeyword(entry.Key, entry.Value);
		}
	}

	/// <summary>
	/// Construct failure table.
	/// </summary>
	/// <exception cref="InvalidOperationException"><see cref="Build" /> has been called previously.</exception>
	private void ConstructFailureStates() {
		if (this.rootState == null) {
			throw new InvalidOperationException("Cannot ConstructFailureStates after Build");
		}

		this.fail = new int[this.size + 1];
		this.output = new IList<int>?[this.size + 1];
		var queue = new Queue<AhoCorasickDoubleArrayTrieState>();

		foreach (AhoCorasickDoubleArrayTrieState depthOneState in this.rootState.States) {
			depthOneState.SetFailure(this.rootState, this.fail);
			queue.Enqueue(depthOneState);
			this.ConstructOutput(depthOneState);
		}

		while (queue.Count > 0) {
			AhoCorasickDoubleArrayTrieState currentState = queue.Dequeue();

			foreach (var transition in currentState.Transitions) {
				AhoCorasickDoubleArrayTrieState targetState = currentState.NextState(transition)
					?? throw new InvalidOperationException("targetState is null");
				queue.Enqueue(targetState);

				AhoCorasickDoubleArrayTrieState traceFailureState = currentState.Failure
					?? throw new InvalidOperationException("traceFailureState is null");
				while (traceFailureState.NextState(transition) == null) {
					traceFailureState = traceFailureState.Failure
						?? throw new InvalidOperationException("traceFailureState is null");
				}

				AhoCorasickDoubleArrayTrieState? newFailureState = traceFailureState.NextState(transition)
					?? throw new InvalidOperationException("newFailureState is null");
				targetState.SetFailure(newFailureState, this.fail);
				targetState.AddEmit(newFailureState.Emit);
				this.ConstructOutput(targetState);
			}
		}
	}

	/// <summary>
	/// Construct output table.
	/// </summary>
	/// <param name="targetState">The target state.</param>
	private void ConstructOutput(AhoCorasickDoubleArrayTrieState targetState) {
		var emit = targetState.Emit;
		if (emit == null || emit.Count == 0) {
			return;
		}

		int[] output = new int[emit.Count];
		int i = 0;
		foreach (var entry in emit) {
			output[i] = entry;
			++i;
		}

		this.output[targetState.Index] = output;
	}

	private void BuildDoubleArrayTrie() {
		if (this.rootState == null) {
			throw new InvalidOperationException("Cannot BuildDoubleArrayTrie after Build");
		}

		this.progress = 0;

		this.Resize(65536 * 32);

		this.@base[0] = 1;
		this.nextCheckPos = 0;

		AhoCorasickDoubleArrayTrieState rootNode = this.rootState;

		var siblings = new List<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>>(rootNode.Success.Count);
		Fetch(rootNode, siblings);
		if (siblings.Count == 0) {
			for (int i = 0; i < this.check.Length; i++) {
				this.check[i] = -1;
			}
		} else {
			this.Insert(siblings);
		}
	}

	/// <summary>
	/// Allocate the memory of the dynamic array.
	/// </summary>
	/// <param name="newSize">The length of the new arrays.</param>
	private void Resize(int newSize) {
		Array.Resize(ref this.@base, newSize);
		Array.Resize(ref this.check, newSize);
		Array.Resize(ref this.used, newSize);
		this.allocSize = newSize;
	}

	/// <summary>
	/// insert the siblings to double array trie
	/// </summary>
	/// <param name="firstSiblings">the initial siblings being inserted</param>
	private void Insert(IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>> firstSiblings) {
		var siblingQueue = new Queue<KeyValuePair<int?, IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>>>>();
		siblingQueue.Enqueue(new KeyValuePair<int?, IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>>>(null, firstSiblings));

		while (siblingQueue.Count > 0) {
			this.Insert(siblingQueue);
		}
	}

	/// <summary>
	/// insert the siblings to double array trie
	/// </summary>
	/// <param name="siblingQueue">a queue holding all siblings being inserted and the position to insert them</param>
	/// <exception cref="NotSupportedException">The total length of the keywords is too large.</exception>
	private void Insert(Queue<KeyValuePair<int?, IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>>>> siblingQueue) {
		KeyValuePair<int?, IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>>> tCurrent = siblingQueue.Dequeue();
		IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>> siblings = tCurrent.Value;

		int begin;
		int pos = Math.Max(siblings[0].Key + 1, this.nextCheckPos) - 1;
		int nonzeroNum = 0;
		int first = 0;

		if (this.allocSize <= pos) {
			this.Resize(pos + 1);
		}

		outer:
		// The goal of this loop body is to find n free space that satisfies base[begin + a1...an] == 0, a1...an is the n nodes in the siblings
		while (true) {
			pos++;

			if (this.allocSize <= pos) {
				this.Resize(pos + 1);
			}

			if (this.check[pos] != 0) {
				nonzeroNum++;
				continue;
			} else if (first == 0) {
				this.nextCheckPos = pos;
				first = 1;
			}

			begin = pos - siblings[0].Key; // The distance of the current position from the first sibling node
			if (this.allocSize <= (begin + siblings[siblings.Count - 1].Key)) {
				// progress can be zero
				// Prevents progress from generating division-by-zero errors
				double toSize = Math.Max(1.05, 1.0 * this.keyLengths.Count / (this.progress + 1)) * this.allocSize;
				const int maxSize = (int)(int.MaxValue * 0.95);
				if (this.allocSize >= maxSize) {
					throw new NotSupportedException("Double array trie is too big.");
				}

				this.Resize((int)Math.Min(toSize, maxSize));
			}

			if (this.used[begin]) {
				continue;
			}

			for (int i = 1; i < siblings.Count; i++) {
				if (this.check[begin + siblings[i].Key] != 0) {
					goto outer;
				}
			}

			break;
		}

		// -- Simple heuristics --
		// if the percentage of non-empty contents in check between the
		// index
		// 'next_check_pos' and 'check' is greater than some constant value
		// (e.g. 0.9),
		// new 'next_check_pos' index is written by 'check'.
		if (1.0 * nonzeroNum / (pos - this.nextCheckPos + 1) >= 0.95) {
			this.nextCheckPos = pos; // Starting from the location next_check_pos to pos, if the space occupied is more than 95%, the next time you insert the node, start the lookup directly from the pos location
		}

		this.used[begin] = true;

		this.size = (this.size > begin + siblings[siblings.Count - 1].Key + 1)
			? this.size
			: begin + siblings[siblings.Count - 1].Key + 1;

		foreach (var sibling in siblings) {
			this.check[begin + sibling.Key] = begin;
		}

		foreach (var sibling in siblings) {
			IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>> newSiblings = new List<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>>(sibling.Value.Success.Count + 1);

			if (Fetch(sibling.Value, newSiblings) == 0) // The termination of a word that is not a prefix for other words is actually a leaf node
			{
				this.@base[begin + sibling.Key] = -sibling.Value.LargestValueId - 1;
				this.progress++;
			} else {
				siblingQueue.Enqueue(new KeyValuePair<int?, IList<KeyValuePair<int, AhoCorasickDoubleArrayTrieState>>>(begin + sibling.Key, newSiblings));
			}

			sibling.Value.Index = begin + sibling.Key;
		}

		// Insert siblings
		int? parentBaseIndex = tCurrent.Key;
		if (parentBaseIndex != null) {
			this.@base[parentBaseIndex.Value] = begin;
		}
	}

	/// <summary>
	/// Free the unnecessary memory.
	/// </summary>
	private void LoseWeight() {
		//tbd: possible optimization for zero-value tail?..
		int[] newBase = new int[this.size + 65535];
		Array.Copy(this.@base, 0, newBase, 0, this.size);
		this.@base = newBase;

		int[] newCheck = new int[this.size + 65535];
		Array.Copy(this.check, 0, newCheck, 0, Math.Min(this.check.Length, newCheck.Length));
		this.check = newCheck;
	}
}

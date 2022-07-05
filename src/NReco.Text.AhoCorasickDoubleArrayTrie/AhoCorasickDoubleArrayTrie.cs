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
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Factory for Aho Corasick algorithm based on Double Array Trie.
/// </summary>
public static class AhoCorasickDoubleArrayTrie {
	/// <summary>
	/// Load automata state from specified binary stream.
	/// </summary>
	/// <typeparam name="TValue">The type of value returned when search strings match.</typeparam>
	/// <param name="input">The data saved by <see cref="AhoCorasickDoubleArrayTrie{TValue}.SaveAsync" />.</param>
	/// <param name="cancellationToken">The cancellation token to abort execution.</param>
	/// <exception cref="ArgumentNullException"><paramref name="input"/> is <c>null</c>.</exception>
	public static async Task<AhoCorasickDoubleArrayTrie<TValue>> LoadAsync<TValue>(Stream input, CancellationToken cancellationToken) {
		if (input == null) {
			throw new ArgumentNullException(nameof(input));
		}

		var (_, ignoreCase, l, @base, check, fail, output, values) = await LoadFromStreamAsync<TValue>(input, cancellationToken).ConfigureAwait(false);
		return new AhoCorasickDoubleArrayTrie<TValue>(ignoreCase, l, @base, check, fail, output, values);
	}

	/// <summary>
	/// Load automata state from specified binary stream. If values are not saved, specified handler is used to restore them.
	/// </summary>
	/// <typeparam name="TValue">The type of value returned when search strings match.</typeparam>
	/// <param name="input">The data saved by <see cref="AhoCorasickDoubleArrayTrie{TValue}.SaveAsync" />.</param>
	/// <param name="loadValueHandler">If data is saved without values, this is used to restore the values.</param>
	/// <param name="cancellationToken">The cancellation token to abort execution.</param>
	/// <exception cref="ArgumentNullException"><paramref name="input"/> is <c>null</c>.</exception>
	public static async Task<AhoCorasickDoubleArrayTrie<TValue>> LoadAsync<TValue>(Stream input, Func<int, TValue>? loadValueHandler, CancellationToken cancellationToken) {
		if (input == null) {
			throw new ArgumentNullException(nameof(input));
		}

		var (_, ignoreCase, l, @base, check, fail, output, values) = await LoadFromStreamAsync<TValue>(input, cancellationToken).ConfigureAwait(false);
		if (values == null && loadValueHandler != null) {
			values = new TValue[l.Length];
			for (int i = 0; i < l.Length; i++) {
				values[i] = loadValueHandler(i);
			}
		}

		return new AhoCorasickDoubleArrayTrie<TValue>(ignoreCase, l, @base, check, fail, output, values);
	}

	private static async Task<(int Size, bool IgnoreCase, int[] KeyLengths, int[] Base, int[] Check, int[] Fail, int[]?[] Output, TValue[]? Values)>
		LoadFromStreamAsync<TValue>(Stream input, CancellationToken cancellationToken) {
		using var binRdr = new Read7BitEncodedBinaryReader(input);
		bool loadValues = true;
		int size = 0;
		bool ignoreCase = false;

		var propsCount = await binRdr.ReadByteAsync(cancellationToken).ConfigureAwait(false);
		for (byte i = 0; i < propsCount; i++) {
			var propName = await binRdr.ReadStringAsync(cancellationToken).ConfigureAwait(false);
			switch (propName) {
				case "saveValues":
					loadValues = await binRdr.ReadBooleanAsync(cancellationToken).ConfigureAwait(false);
					break;
				case "size":
					size = await binRdr.ReadInt32Async(cancellationToken).ConfigureAwait(false);
					break;
				case "ignoreCase":
					ignoreCase = await binRdr.ReadBooleanAsync(cancellationToken).ConfigureAwait(false);
					break;
			}
		}

		int[] keyLengths = await binRdr.ReadIntListAsync(cancellationToken).ConfigureAwait(false);
		int[] @base = await binRdr.ReadIntListAsync(cancellationToken).ConfigureAwait(false);
		int[] check = await binRdr.ReadIntListAsync(cancellationToken).ConfigureAwait(false);
		int[] fail = await binRdr.ReadIntListAsync(cancellationToken).ConfigureAwait(false);
		int[]?[] output = await binRdr.ReadIntIntListAsync(cancellationToken).ConfigureAwait(false);
		TValue[]? values = null;

		if (loadValues) {
			var vType = typeof(TValue);
			var typeCode = Type.GetTypeCode(vType);
			Func<Read7BitEncodedBinaryReader, CancellationToken, Task<object?>> readElem;
			if (typeCode != TypeCode.Object && (int)typeCode < Read7BitEncodedBinaryReader.TypeCodeReaders.Length) {
				readElem = Read7BitEncodedBinaryReader.TypeCodeReaders[(int)typeCode];
			} else {
				throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Cannot read values of type '{0}', only primitive types are supported.", vType));
			}

			var length = await binRdr.ReadIntAsync(cancellationToken).ConfigureAwait(false);
			values = new TValue[length];
			for (int i = 0; i < length; i++) {
				values[i] = (TValue)(await readElem(binRdr, cancellationToken).ConfigureAwait(false)
					?? throw new NotSupportedException("Unexpected null for value of primitive type"));
			}
		}

		return (size, ignoreCase, keyLengths, @base, check, fail, output, values);
	}
}

/// <summary>
/// An implementation of Aho Corasick algorithm based on Double Array Trie.
/// </summary>
/// <typeparam name="TValue">The type of value returned when search strings match.</typeparam>
public class AhoCorasickDoubleArrayTrie<TValue> {
	/// <summary>
	/// Check array of the Double Array Trie structure
	/// </summary>
	private readonly IList<int> check;

	/// <summary>
	/// Base array of the Double Array Trie structure
	/// </summary>
	private readonly IList<int> @base;

	/// <summary>
	/// Fail table of the Aho Corasick automata
	/// </summary>
	private readonly IList<int> fail;

	/// <summary>
	/// Output table of the Aho Corasick automata
	/// </summary>
	private readonly IList<IList<int>?> output;

	/// <summary>
	/// Outer value array
	/// </summary>
	private readonly IList<TValue>? values;

	/// <summary>
	/// The length of every key.
	/// </summary>
	private readonly IList<int> keyLengths;

	private readonly bool ignoreCase;

	internal AhoCorasickDoubleArrayTrie(bool ignoreCase, IList<int> l, IList<int> @base, IList<int> check, IList<int> fail, IList<IList<int>?> output, IList<TValue>? values) {
		this.ignoreCase = ignoreCase;
		this.keyLengths = l;
		this.@base = @base;
		this.check = check;
		this.fail = fail;
		this.output = output;
		this.values = values;
	}

	/// <summary>
	/// Parse text and match all substrings.
	/// </summary>
	/// <param name="text">The text</param>
	/// <returns>a list of matches</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
	public IList<AhoCorasickDoubleArrayTrieHit<TValue>> ParseText(string text) {
		if (text == null) {
			throw new ArgumentNullException(nameof(text));
		}

		int position = 1;
		int currentState = 0;
		IList<AhoCorasickDoubleArrayTrieHit<TValue>> collectedEmits = new List<AhoCorasickDoubleArrayTrieHit<TValue>>();
		bool ignoreCase = this.ignoreCase;
		for (int i = 0; i < text.Length; ++i) {
			char character = text[i];
			if (ignoreCase) {
				character = char.ToUpperInvariant(character);
			}

			currentState = this.GetState(currentState, character);
			this.StoreEmits(position, currentState, collectedEmits);
			++position;
		}

		return collectedEmits;
	}

	/// <summary>
	/// Parse text and match substrings (cancellable).
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="processor"/> is <c>null</c>.</exception>
	public void ParseText(string text, Func<AhoCorasickDoubleArrayTrieHit<TValue>, bool> processor) {
		if (text == null) {
			throw new ArgumentNullException(nameof(text));
		}

		if (processor == null) {
			throw new ArgumentNullException(nameof(processor));
		}

		this.ParseTextInner(text, processor);
	}

	private void ParseTextInner(string text, Func<AhoCorasickDoubleArrayTrieHit<TValue>, bool> processor) {
		int position = 1;
		int currentState = 0;
		bool ignoreCase = this.ignoreCase;
		for (int chIdx = 0; chIdx < text.Length; ++chIdx) {
			char character = text[chIdx];
			if (ignoreCase) {
				character = char.ToUpperInvariant(character);
			}

			currentState = this.GetState(currentState, character);
			IList<int>? hitArray = this.output[currentState];
			if (hitArray != null) {
				foreach (int hit in hitArray) {
					TValue? value = this.values == null ? default : this.values[hit];
					if (!processor(new AhoCorasickDoubleArrayTrieHit<TValue>(position - this.keyLengths[hit], position, value, hit))) {
						return;
					}
				}
			}

			++position;
		}
	}

	/// <summary>
	/// Parse text and match all substrings with a handler.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="processor">A processor which handles matches.</param>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="processor"/> is <c>null</c>.</exception>
	public void ParseText(string text, Action<AhoCorasickDoubleArrayTrieHit<TValue>> processor) {
		if (text == null) {
			throw new ArgumentNullException(nameof(text));
		}

		if (processor == null) {
			throw new ArgumentNullException(nameof(processor));
		}

		this.ParseTextInner(text, (hit) => { processor(hit); return true; });
	}

	/// <summary>
	/// Parse text represented as char array.
	/// </summary>
	/// <param name="text">The text represented by a char array</param>
	/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="processor"/> is <c>null</c>.</exception>
	public void ParseText(IList<char> text, Func<AhoCorasickDoubleArrayTrieHit<TValue>, bool> processor) {
		if (text == null) {
			throw new ArgumentNullException(nameof(text));
		}

		if (processor == null) {
			throw new ArgumentNullException(nameof(processor));
		}

		this.ParseTextInner(text, 0, text.Count, processor);
	}

	/// <summary>
	/// Parse text in a char array buffer.
	/// </summary>
	/// <param name="text">char array buffer.</param>
	/// <param name="start">text start position.</param>
	/// <param name="length">text length in the char array.</param>
	/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="processor"/> is <c>null</c>.</exception>
	public void ParseText(IList<char> text, int start, int length, Func<AhoCorasickDoubleArrayTrieHit<TValue>, bool> processor) {
		if (text == null) {
			throw new ArgumentNullException(nameof(text));
		}

		if (processor == null) {
			throw new ArgumentNullException(nameof(processor));
		}

		this.ParseTextInner(text, start, length, processor);
	}

	private void ParseTextInner(IList<char> text, int start, int length, Func<AhoCorasickDoubleArrayTrieHit<TValue>, bool> processor) {
		int position = 1;
		int currentState = 0;
		int end = start + length;
		bool ignoreCase = this.ignoreCase;
		for (int chIdx = start; chIdx < end; chIdx++) {
			char character = text[chIdx];
			if (ignoreCase) {
				character = char.ToUpperInvariant(character);
			}

			currentState = this.GetState(currentState, character);
			IList<int>? hitArray = this.output[currentState];
			if (hitArray != null) {
				foreach (var hit in hitArray) {
					TValue? value = this.values == null ? default : this.values[hit];
					if (!processor(new AhoCorasickDoubleArrayTrieHit<TValue>(position - this.keyLengths[hit], position, value, hit))) {
						return;
					}
				}
			}

			++position;
		}
	}

	/// <summary>
	/// Checks that string contains at least one substring
	/// </summary>
	/// <param name="text">source text to check</param>
	/// <returns><see langword="true" /> if string contains at least one substring</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
	public bool Matches(string text) {
		if (text == null) {
			throw new ArgumentNullException(nameof(text));
		}

		int currentState = 0;
		bool ignoreCase = this.ignoreCase;
		for (int i = 0; i < text.Length; ++i) {
			char character = text[i];
			if (ignoreCase) {
				character = char.ToUpperInvariant(character);
			}

			currentState = this.GetState(currentState, character);
			IList<int>? hitArray = this.output[currentState];
			if (hitArray != null) {
				return true;
			}
		}

		return false;
	}

	/**
	 * Search first match in string
	 *
	 * @param text source text to check
	 * @return first match or {@code null} if there are no matches
	 */
	public AhoCorasickDoubleArrayTrieHit<TValue>? FindFirst(string text) {
		if (text == null) {
			throw new ArgumentNullException(nameof(text));
		}

		int position = 1;
		int currentState = 0;
		bool ignoreCase = this.ignoreCase;
		for (int i = 0; i < text.Length; ++i) {
			char character = text[i];
			if (ignoreCase) {
				character = char.ToUpperInvariant(character);
			}

			currentState = this.GetState(currentState, character);
			IList<int>? hitArray = this.output[currentState];
			if (hitArray != null) {
				int hitIndex = hitArray[0];
				TValue? value = this.values == null ? default : this.values[hitIndex];
				return new AhoCorasickDoubleArrayTrieHit<TValue>(position - this.keyLengths[hitIndex], position, value, hitIndex);
			}

			++position;
		}

		return null;
	}

	/// <summary>
	/// Gets the size of the keywords that could be matched by automata.
	/// </summary>
	public int Count => this.keyLengths.Count;

	/// <summary>
	/// Gets value by a string key.
	/// </summary>
	/// <param name="key">The key (substring that can be matched by automata).</param>
	/// <returns>The value.</returns>
	public TValue? this[string key] {
		get {
			int index = this.ExactMatchSearch(key);
			if (index >= 0) {
				return this.values == null ? default : this.values[index];
			}

			return default;
		}
	}

	/// <summary>
	/// Pick the value by index in value array.
	/// </summary>
	/// <param name="index">The index.</param>
	/// <returns>The value.</returns>
	/// <remarks>Notice that to be more efficiently, this method DO NOT check the parameter.</remarks>
	public TValue? this[int index] => this.values == null ? default : this.values[index];

	/// <summary>
	/// Transmit state, supports failure function
	/// </summary>
	/// <param name="currentState">The current state.</param>
	/// <param name="character">The current character from the text being searched.</param>
	/// <returns>Returns the next state.</returns>
	private int GetState(int currentState, char character) {
		int newCurrentState = this.TransitionWithRoot(currentState, character);
		while (newCurrentState == -1) {
			currentState = this.fail[currentState];
			newCurrentState = this.TransitionWithRoot(currentState, character);
		}

		return newCurrentState;
	}

	/// <summary>
	/// Store output
	/// </summary>
	/// <param name="position">The current index of the text being search.</param>
	/// <param name="currentState">The current state.</param>
	/// <param name="collectedEmits">Store the hit in this list.</param>
	private void StoreEmits(int position, int currentState, IList<AhoCorasickDoubleArrayTrieHit<TValue>> collectedEmits) {
		IList<int>? hitArray = this.output[currentState];
		if (hitArray != null) {
			foreach (int hit in hitArray) {
				TValue? value = this.values == null ? default : this.values[hit];
				collectedEmits.Add(new AhoCorasickDoubleArrayTrieHit<TValue>(position - this.keyLengths[hit], position, value, hit));
			}
		}
	}

	/// <summary>
	/// Transition of a state, if the state is root and it failed, then returns the root
	/// </summary>
	/// <param name="nodePos">The current state.</param>
	/// <param name="character">The current character from the text being searched.</param>
	private int TransitionWithRoot(int nodePos, char character) {
		int b = this.@base[nodePos];
		int p = b + character + 1;
		if (b != this.check[p]) {
			if (nodePos == 0) {
				return 0;
			}

			return -1;
		}

		return p;
	}

	/// <summary>
	/// Match exactly by a key
	/// </summary>
	/// <param name="key">the key</param>
	/// <returns>the index of the key, you can use it as a perfect hash function.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
	public int ExactMatchSearch(string key) {
		if (key == null) {
			throw new ArgumentNullException(nameof(key));
		}

		return this.ExactMatchSearch(key, 0, 0, 0);
	}

	private int ExactMatchSearch(string key, int pos, int len, int nodePos) {
		if (len <= 0) {
			len = key.Length;
		}

		if (nodePos <= 0) {
			nodePos = 0;
		}

		const int result = -1;
		return this.GetMatched(pos, len, result, key, this.@base[nodePos]);
	}

	private int GetMatched(int pos, int len, int result, string key, int b1) {
		int b = b1;
		int p;
		bool ignoreCase = this.ignoreCase;
		for (int i = pos; i < len; i++) {
			char character = key[i];
			if (ignoreCase) {
				character = char.ToUpperInvariant(character);
			}

			p = b + character + 1;
			if (b == this.check[p]) {
				b = this.@base[p];
			} else {
				return result;
			}
		}

		p = b; // transition through '\0' to check if it's the end of a word
		int n = this.@base[p];
		if (b == this.check[p]) // yes, it is.
		{
			result = -n - 1;
		}

		return result;
	}

	/// <summary>
	/// Save automata state into binary stream.
	/// </summary>
	/// <param name="output">Save the built data structure in this stream.</param>
	/// <param name="saveValues"><see langword="true" /> to save the values that are returned when matched. If <see langword="false" />, hits will use the default value of the type, e.g. null or zero, for each hit.</param>
	/// <param name="cancellationToken">The cancellation token to abort execution.</param>
	/// <exception cref="ArgumentNullException"><paramref name="output"/> is <c>null</c>.</exception>
	/// <exception cref="NotSupportedException"></exception>
	public async Task SaveAsync(Stream output, bool saveValues, CancellationToken cancellationToken) {
		if (output == null) {
			throw new ArgumentNullException(nameof(output));
		}

		saveValues = saveValues && this.values != null;
		using var binWr = new Write7BitEncodedBinaryWriter(output);
		await binWr.WriteAsync((byte)3, cancellationToken).ConfigureAwait(false); // number of single-value props
		await binWr.WriteAsync("saveValues", cancellationToken).ConfigureAwait(false);
		await binWr.WriteAsync(saveValues, cancellationToken).ConfigureAwait(false);
		await binWr.WriteAsync("size", cancellationToken).ConfigureAwait(false);
		await binWr.WriteAsync(this.@base.Count - 65535, cancellationToken).ConfigureAwait(false);
		await binWr.WriteAsync("ignoreCase", cancellationToken).ConfigureAwait(false);
		await binWr.WriteAsync(this.ignoreCase, cancellationToken).ConfigureAwait(false);

		await binWr.WriteIntListAsync(this.keyLengths, cancellationToken).ConfigureAwait(false);
		await binWr.WriteIntListAsync(this.@base, cancellationToken).ConfigureAwait(false);
		await binWr.WriteIntListAsync(this.check, cancellationToken).ConfigureAwait(false);
		await binWr.WriteIntListAsync(this.fail, cancellationToken).ConfigureAwait(false);
		await binWr.WriteIntIntListAsync(this.output, cancellationToken).ConfigureAwait(false);

		if (saveValues && this.values != null) {
			var vType = typeof(TValue);
			var typeCode = Type.GetTypeCode(vType);
			Func<Write7BitEncodedBinaryWriter, object, CancellationToken, Task> wrElem;
			if (typeCode != TypeCode.Object && (int)typeCode < Write7BitEncodedBinaryWriter.TypeCodeWriters.Length) {
				wrElem = Write7BitEncodedBinaryWriter.TypeCodeWriters[(int)typeCode];
			} else {
				throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Cannot write values of type '{0}', only primitive types are supported.", vType));
			}
			await binWr.WriteIntAsync(this.values.Count, cancellationToken).ConfigureAwait(false);
			for (int i = 0; i < this.values.Count; i++) {
				var value = this.values[i] ?? throw new NotSupportedException("Unexpected null for value of primitive type");
				await wrElem(binWr, value, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}

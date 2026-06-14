using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {
    //キーとインデックス両方で取れるList兼Dictionary
    public class KeyedList<TKey, TValue> : IEnumerable<TValue> where TKey : notnull {
        public int Count => _list.Count;
        private readonly List<TValue> _list = new();
        private readonly Dictionary<TKey, int> _indexMap = new();
        // キーと値のペアを保持するための関数（必要に応じて定義）
        private readonly Func<TValue, TKey> _keySelector;

        public KeyedList(Func<TValue, TKey> keySelector) => _keySelector = keySelector;

        // インデックスアクセスとキーアクセスの両立
        // インデックスでのアクセスと代入
        public TValue this[int index] {
            get => _list[index];
            set {
                // 範囲チェックを忘れずに（ここをサボると実行時に例外が出るので）
                if (index < 0 || index >= _list.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                _list[index] = value;
            }
        }
        //キーでのアクセスと代入
        public TValue this[TKey key] {
            get => _list[_indexMap[key]];
            set {
                // 既存のキーであれば値を更新する
                if (_indexMap.ContainsKey(key)) {
                    int index = _indexMap[key];
                    _list[index] = value;
                } else {
                    // 新規なら Add する
                    Add(value);
                }
            }
        }

        public void Add(TValue item) {
            // 既存キーのチェックを入れるとより堅牢に
            var key = _keySelector(item);
            if (_indexMap.ContainsKey(key))
                throw new InvalidOperationException($"Key '{key}' already exists.");

            _indexMap[key] = _list.Count;
            _list.Add(item);
        }
        
        // foreachで KeyValuePair として回せるようにする（Dictionary互換）
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            foreach (var item in _list) {
                yield return new KeyValuePair<TKey, TValue>(_keySelector(item), item);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() {
            throw new NotImplementedException();
        }
    }
}

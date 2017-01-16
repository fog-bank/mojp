# MTGO Japanese Card Text Preview
[Magic: The Gathering Online](http://www.mtgo.com/) で表示されているカードに対応して、日本語テキストを自動で表示するアプリです。

![Screenshot v1.3.0](https://github.com/fog-bank/mojp/blob/master/images/screenshot.png)

## 必要条件
.NET Framework 4.6 以上（Windows 10 なら標準でインストール済みです）

## 使い方
1. MO を起動し、COLLECTION 画面や対戦・トレード中に Preview Pane を表示するようにします（最小化しても OK）。
2. MO 上で調べたいカードにマウスカーソルを移動させます。
3. このアプリの表示が変わり、そのカードの日本語テキストが表示されます。

### 補足
* Preview Pane を表示するには、ACCOUNT 画面の「Display & Sound Settings」内の右下にある「Display Card Preview Window」にチェックを入れます。詳細は[こちら](https://github.com/fog-bank/mojp/wiki/Preview-Pane-%E3%82%92%E8%A1%A8%E7%A4%BA%E3%81%95%E3%81%9B%E3%82%8B%E6%96%B9%E6%B3%95)。
* トークンや紋章などには対応しておらず、何も表示されません。詳細は[こちら](https://github.com/fog-bank/mojp/wiki/%E4%B8%8D%E5%85%B7%E5%90%88)。
* あくまで参考程度のご利用でお願い致します。このアプリを原因とするプレイミスには責任を負いかねます。

### その他の機能
* 右クリックメニューからカード名をコピーしたり、[MTG Wiki](http://mtgwiki.com/) のカード評価ページに移動したりできます。
* 右上の歯車アイコンのボタンから、このアプリで使用するフォントとそのサイズを設定できます。その他、ウィンドウサイズなどの設定は、%LocalAppData%\co3366353 フォルダに保存されます。

## 仕組み
[UI Automation](https://msdn.microsoft.com/ja-jp/library/ms753388.aspx) API を利用して、Preview Pane 内のテキストを検索しています。メインロジックは [MainViewModel.cs](https://github.com/fog-bank/mojp/blob/master/mojp/MainViewModel.cs) 内の OnCapture メソッド以下です。

## リソース
* このアプリで表示する日本語テキストは、[WHISPER](http://whisper.wisdom-guild.net/) の検索結果をテキストファイルに保存したものに基づいています。
* アイコンの一部は Iconfinder \[[1](https://www.iconfinder.com/icons/6000/book_dictionary_learn_school_translate_icon#size=128), [2](https://www.iconfinder.com/icons/285680/camera_icon#size=16)\] を利用しています。

### 手動でカードデータを更新する方法
1. リポジトリから [appendix.xml](https://github.com/fog-bank/mojp/blob/master/mojp/appendix.xml) をダウンロードして、アプリケーション フォルダに保存しておく。
2. Wisdom Guild さんのカード検索で、 次元、現象、計略、策略以外のカードタイプにチェックを入れ、さらに一番下の出力形式をテキストにして検索する。
3. 表示されたテキストを、エンコード形式を Shift-JIS にして保存する。
4. 本アプリの設定画面を開き、一番下の開発用メニューから「検索結果テキストの読み込み」ボタンで保存したテキストを読み込む。
5. チェックマークが表示されたら完了。アプリケーション フォルダにある cards.xml が変更されているはずです。

カードセットを絞らない場合、1.6 万件以上のカードがヒットし、テキストのサイズは 6 MB を超えますので、この方法で頻繁に検索しないでください。Wisdom Guild さんのサーバーに対し著しく負荷をかける行為は禁止されています。
# MTGO Japanese Card Text Preview
[Magic: The Gathering Online](http://www.mtgo.com/) で表示されているカードに対応して、日本語テキストを自動で表示するアプリです。

![Screenshot v1.2.0](https://cloud.githubusercontent.com/assets/22735830/19632786/101c571a-99e5-11e6-911b-2e4f5db34240.png)

## 必要条件
.NET Framework 4.6（Windows 10 なら標準でインストール済みです）

## 使い方
1. MO を起動し、COLLECTION 画面や、対戦・トレード中に Preview Pane を表示するようにします。
2. 準備完了と表示されるまでしばらく待ちます。
3. MO で調べたいカードにマウスカーソルを移動させます。
4. このアプリの表示が変わり、そのカードの日本語テキストが表示されます。

### 補足
* Preview Pane を表示するには、ACCOUNT 画面の「Display & Sound Settings」内の右下にある「Display Card Preview Window」にチェックを入れます。詳細は[こちら](https://github.com/fog-bank/mojp/wiki/Preview-Pane-%E3%82%92%E8%A1%A8%E7%A4%BA%E3%81%95%E3%81%9B%E3%82%8B%E6%96%B9%E6%B3%95)。
* Preview Pane が邪魔な場合は最小化してしまって問題ありません。
* トークンや紋章などには対応しておらず、何も表示されません。詳細は[こちら](https://github.com/fog-bank/mojp/wiki/%E4%B8%8D%E5%85%B7%E5%90%88)。
* あくまで参考程度のご利用でお願い致します。このアプリを原因とするプレイミスには責任を負いかねます。

### その他の機能
* 右クリックメニューからカード名をコピーしたり、[MTG Wiki](http://mtgwiki.com/) のカード評価ページに移動したりできます。
* 右上の歯車アイコンのボタンから、このアプリで使用するフォントとそのサイズを設定できます。その他、ウィンドウサイズなどの設定は、%LocalAppData%\co3366353 フォルダに保存されます。

## 仕組み
[UI Automation](https://msdn.microsoft.com/ja-jp/library/ms753388.aspx) API を利用して、Preview Pane 内のテキストを検索しています。メインロジックは MainViewModel.cs 内の OnCapture メソッド以下です。

## リソース
* このアプリで表示する日本語テキストは、[WHISPER](http://whisper.wisdom-guild.net/) の検索結果をテキストファイルに保存したものに基づいています。
* このアプリのアイコンは [Iconfinder](https://www.iconfinder.com/icons/6000/book_dictionary_learn_school_translate_icon#size=128) のものを利用しています。

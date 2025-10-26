# SvgCreator Developer Notes

このリポジトリで OpenCvSharp をクロスプラットフォームに利用するために実施した手順と、その背景をまとめます。特に Ubuntu 24.04 で FFmpeg 60 系ライブラリを利用する場合は下記の手順を実行してください。

## 1. Linux ランタイム依存の整備

```
sudo apt update
sudo apt install --no-install-recommends \\
  build-essential cmake git ccache pkg-config \\
  libgtk-3-dev libtbb-dev libatlas-base-dev libeigen3-dev \\
  libavcodec-dev libavformat-dev libswscale-dev libavutil-dev \\
  libdc1394-dev libv4l-dev libxvidcore-dev libx264-dev \\
  libopenexr-dev libtiff-dev libjpeg-dev libpng-dev \\
  libopenjp2-7-dev libwebp-dev libtesseract-dev
```

- **意味**: OpenCV 4.12 のビルド時に必要なコンパイラ・ヘッダー・FFmpeg 6.1 系・OpenEXR 3.1 系などの依存を揃えます。これらを先に入れておくことでビルド済みの `libOpenCvSharpExtern.so` がホストの最新ライブラリとリンクされます。

## 2. OpenCV 4.12.0 のビルド

```
mkdir -p ~/opencv-src && cd ~/opencv-src
git clone --branch 4.12.0 https://github.com/opencv/opencv.git
git clone --branch 4.12.0 https://github.com/opencv/opencv_contrib.git

mkdir opencv-build && cd opencv-build
cmake ../opencv \\
  -D CMAKE_BUILD_TYPE=Release \\
  -D CMAKE_INSTALL_PREFIX=/usr/local \\
  -D OPENCV_GENERATE_PKGCONFIG=ON \\
  -D OPENCV_ENABLE_NONFREE=ON \\
  -D OPENCV_EXTRA_MODULES_PATH=../opencv_contrib/modules \\
  -D WITH_TBB=ON \\
  -D WITH_FFMPEG=ON \\
  -D WITH_OPENEXR=ON \\
  -D WITH_GTK=ON \\
  -D BUILD_TESTS=OFF \\
  -D BUILD_PERF_TESTS=OFF

sudo cmake --build . --target install -j$(nproc)
sudo ldconfig
```

- **意味**: OpenCV をシステムの FFmpeg 60 系とリンクさせて `/usr/local` にインストールします。`ldconfig` で共有ライブラリキャッシュを更新し、`/usr/local/lib` が検索パスにあることを確認します。

## 3. OpenCvSharpExtern のビルド

```
cd ~/opencv-src
git clone --branch 4.12.0 https://github.com/shimat/opencvsharp.git
cd opencvsharp/src
mkdir build && cd build

cmake .. \\
  -D CMAKE_BUILD_TYPE=Release \\
  -D CMAKE_INSTALL_PREFIX=/usr/local \\
  -D BUILD_SHARED_LIBS=ON \\
  -D OpenCV_DIR=/usr/local/lib/cmake/opencv4

cmake --build . --config Release -j$(nproc)
sudo cmake --install .
sudo ldconfig
```

- **意味**: OpenCV 4.12 と同じ環境で `libOpenCvSharpExtern.so` をビルドし `/usr/local/lib` に配置します。これにより FFmpeg 60 系との互換性が保たれたネイティブブリッジを利用できます。

## 4. NuGet ランタイム依存の調整

- `src/SvgCreator.Core/SvgCreator.Core.csproj` と `tests/SvgCreator.Core.Tests/SvgCreator.Core.Tests.csproj` から `OpenCvSharp4.runtime.linux-x64` を削除しています。
- **意味**: NuGet ランタイムを削除することで、`runtimes/linux-x64/native/libOpenCvSharpExtern.so` が生成されなくなり、P/Invoke はホストにインストール済みの `/usr/local/lib/libOpenCvSharpExtern.so` を直接読み込みます。これにより FFmpeg 58 系とのリンクを強制されることがなくなります。
- Windows ビルドのために `OpenCvSharp4.runtime.win` は維持しています。Linux 側で自前のバイナリを使う構成と共存させるための最小構成です。

## 5. 動作確認

```
dotnet test
```

## 備考
- `/usr/local/lib` は既定でライブラリ検索パスに含まれているため、追加の設定は不要ですが、環境によっては `sudo ldconfig` を実行してください。

上記の手順を共有し、Linux 環境でも Windows 同等に OpenCvSharp を安定稼働させることを目的としています。

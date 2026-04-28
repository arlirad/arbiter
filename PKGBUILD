pkgname=arbiter
pkgver=3.0.0
pkgrel=2
pkgdesc=""
arch=('x86_64')
license=('MIT')
makedepends=('dotnet-sdk')
source=("git+https://github.com/arlirad/arbiter")
sha256sums=('SKIP')
options=(!strip !debug)

build() {
    cd "$srcdir/arbiter"

    dotnet publish src/Arbiter/Arbiter.csproj -c Release -r linux-x64 --self-contained -o "$srcdir/publish"
}

package() {
    install -dm755 "$pkgdir/usr/share/$pkgname"
    install -dm755 "$pkgdir/usr/bin"

    cp -r $srcdir/publish/* "$pkgdir/usr/share/$pkgname"

    ln -s "../share/$pkgname/Arbiter" "$pkgdir/usr/bin/$pkgname"
}

pkgname=arbiter
pkgver=3.0.0
pkgrel=1
pkgdesc=""
arch=('x86_64')
license=('MIT')
depends=('opencv' 'lua')
makedepends=('dotnet-sdk-preview-bin' 'dotnet-runtime-preview-bin')
source=("git+https://github.com/arlirad/arbiter")
sha256sums=('SKIP')
options=(!strip !debug)

build() {
    cd "$srcdir/arbiter"

    dotnet restore
    dotnet publish -c Release -o "$srcdir/publish"
}

package() {
    install -dm755 "$pkgdir/usr/share/$pkgname"
    install -dm755 "$pkgdir/usr/bin"

    cp -r $srcdir/publish/* "$pkgdir/usr/share/$pkgname"

    ln -s "../share/$pkgname/Arbiter" "$pkgdir/usr/bin/$pkgname"
}

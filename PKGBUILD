pkgname=arbiter
pkgver=3.3.0
pkgrel=1
pkgdesc=""
arch=('x86_64')
license=('MIT')
makedepends=('dotnet-sdk' 'git')
source=("git+https://github.com/arlirad/arbiter")
sha256sums=('SKIP')
options=(!strip !debug)

pkgver() {
    cd "$srcdir/arbiter"
    git describe --tags --abbrev=0 | sed 's/^v//'
}

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

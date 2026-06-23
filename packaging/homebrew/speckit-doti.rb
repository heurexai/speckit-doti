# Homebrew formula for speckit-doti. Lives in the tap repo heurexai/homebrew-tap as
# Formula/speckit-doti.rb; this copy is the source of truth. After a release, fill the two
# sha256 values from the published archives (see packaging/PUBLISHING.md) and push to the tap.
#   brew install heurexai/tap/speckit-doti
class SpeckitDoti < Formula
  desc "Agentic .NET spec-driven development starter kit (doti workflow)"
  homepage "https://github.com/heurexai/speckit-doti"
  version "0.3.1"
  license "MIT"

  on_macos do
    on_arm do
      url "https://github.com/heurexai/speckit-doti/releases/download/v0.3.1/speckit-doti-v0.3.1-osx-arm64.tar.gz"
      sha256 "0864d48753855e6120d5333fae6cd18a4fae7eebff3e661943a8440915866215"
    end
  end

  on_linux do
    on_intel do
      url "https://github.com/heurexai/speckit-doti/releases/download/v0.3.1/speckit-doti-v0.3.1-linux-x64.tar.gz"
      sha256 "71ab1c7911d3d302e544ce0a426a73d3544089dfe0b4cb1722bf08dfb00d8e99"
    end
  end

  # The generated solution is built with the .NET SDK; hx itself is self-contained.
  depends_on "dotnet" => :optional
  depends_on "git" => :optional

  def install
    # The archive is the hx launcher plus its payload (template, doti, source, manifests, grammars);
    # hx resolves the payload relative to its own location, so keep them together in libexec.
    libexec.install Dir["*"]
    (bin/"hx").write <<~SH
      #!/bin/bash
      exec "#{libexec}/hx" "$@"
    SH
    chmod 0755, bin/"hx"
  end

  test do
    assert_match(/new|describe|hx/i, shell_output("#{bin}/hx describe 2>&1", 0))
  end
end

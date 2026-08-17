"""Tests for the output/hash-verification/archiving config model additions (#273)."""

from bloqr_compiler.config import (
    ArchivingSettings,
    CompilerConfiguration,
    FilterSource,
    HashVerificationSettings,
    OutputSettings,
)


class TestOutputSettingsRoundTrip:
    def test_from_dict_and_to_dict_round_trip(self) -> None:
        data = {"path": "output/list.txt", "conflictStrategy": "overwrite"}

        settings = OutputSettings.from_dict(data)

        assert settings.path == "output/list.txt"
        assert settings.conflict_strategy == "overwrite"
        assert settings.to_dict() == data

    def test_default_conflict_strategy_is_omitted_from_output(self) -> None:
        settings = OutputSettings(path="output/list.txt")

        assert "conflictStrategy" not in settings.to_dict()


class TestHashVerificationSettingsRoundTrip:
    def test_from_dict_and_to_dict_round_trip(self) -> None:
        data = {
            "mode": "strict",
            "requireHashesForRemote": True,
            "failOnMismatch": True,
            "hashDatabasePath": ".hashes.json",
        }

        settings = HashVerificationSettings.from_dict(data)

        assert settings.mode == "strict"
        assert settings.require_hashes_for_remote is True
        assert settings.fail_on_mismatch is True
        assert settings.hash_database_path == ".hashes.json"
        assert settings.to_dict() == data


class TestArchivingSettingsRoundTrip:
    def test_from_dict_and_to_dict_round_trip(self) -> None:
        data = {"enabled": True, "mode": "interactive", "retentionDays": 30}

        settings = ArchivingSettings.from_dict(data)

        assert settings.enabled is True
        assert settings.mode == "interactive"
        assert settings.retention_days == 30
        assert settings.to_dict() == data

    def test_defaults_are_omitted_from_output(self) -> None:
        settings = ArchivingSettings()

        assert settings.to_dict() == {}


class TestCompilerConfigurationWithNewBlocks:
    def _base_config(self, **overrides) -> CompilerConfiguration:
        return CompilerConfiguration(
            name="Test",
            sources=[FilterSource(source="test.txt")],
            **overrides,
        )

    def test_from_dict_parses_all_three_blocks(self) -> None:
        data = {
            "name": "Test",
            "sources": [{"source": "test.txt"}],
            "output": {"path": "out.txt", "conflictStrategy": "rename"},
            "hashVerification": {"mode": "warning", "hashDatabasePath": ".hashes.json"},
            "archiving": {"enabled": True, "retentionDays": 90},
        }

        config = CompilerConfiguration.from_dict(data)

        assert config.output.path == "out.txt"
        assert config.hash_verification.mode == "warning"
        assert config.archiving.enabled is True

    def test_from_dict_leaves_blocks_none_when_absent(self) -> None:
        config = CompilerConfiguration.from_dict({"name": "Test", "sources": [{"source": "test.txt"}]})

        assert config.output is None
        assert config.hash_verification is None
        assert config.archiving is None

    def test_to_dict_round_trips_through_from_dict(self) -> None:
        config = self._base_config(
            output=OutputSettings(path="out.txt"),
            hash_verification=HashVerificationSettings(mode="strict", hash_database_path=".hashes.json"),
            archiving=ArchivingSettings(enabled=True),
        )

        round_tripped = CompilerConfiguration.from_dict(config.to_dict())

        assert round_tripped.output.path == "out.txt"
        assert round_tripped.hash_verification.mode == "strict"
        assert round_tripped.archiving.enabled is True

    def test_validate_rejects_invalid_conflict_strategy(self) -> None:
        config = self._base_config(output=OutputSettings(path="out.txt", conflict_strategy="delete"))

        result = config.validate()

        assert not result.is_valid
        assert any("conflict strategy" in e for e in result.errors)

    def test_validate_rejects_invalid_hash_verification_mode(self) -> None:
        config = self._base_config(
            hash_verification=HashVerificationSettings(mode="paranoid", hash_database_path=".hashes.json")
        )

        result = config.validate()

        assert not result.is_valid
        assert any("hash verification mode" in e for e in result.errors)

    def test_validate_warns_when_hash_verification_enabled_without_database_path(self) -> None:
        config = self._base_config(hash_verification=HashVerificationSettings(mode="warning"))

        result = config.validate()

        assert result.is_valid
        assert any("hashDatabasePath" in w for w in result.warnings)

    def test_validate_rejects_invalid_archiving_mode(self) -> None:
        config = self._base_config(archiving=ArchivingSettings(mode="manual"))

        result = config.validate()

        assert not result.is_valid
        assert any("archiving mode" in e for e in result.errors)

    def test_validate_rejects_zero_retention_days(self) -> None:
        config = self._base_config(archiving=ArchivingSettings(retention_days=0))

        result = config.validate()

        assert not result.is_valid
        assert any("Retention days" in e for e in result.errors)

    def test_validate_passes_for_correct_new_blocks(self) -> None:
        config = self._base_config(
            output=OutputSettings(path="out.txt", conflict_strategy="rename"),
            hash_verification=HashVerificationSettings(mode="warning", hash_database_path=".hashes.json"),
            archiving=ArchivingSettings(enabled=True, mode="automatic", retention_days=90),
        )

        result = config.validate()

        assert result.is_valid

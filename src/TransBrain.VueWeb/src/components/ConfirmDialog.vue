<script setup lang="ts">
import { useI18n } from '../i18n';

/**
 * The delete confirmation of the vehicle list, reusable: `testIdPrefix` "driver-delete" yields
 * the buttons "driver-delete-cancel" and "driver-delete-confirm".
 */
defineProps<{
    title: string;
    message: string;
    testIdPrefix: string;
    confirmLabel?: string;
}>();

const emit = defineEmits<{ confirm: [] }>();
const open = defineModel<boolean>({ required: true });
const { t } = useI18n();

function confirm(): void {
    open.value = false;
    emit('confirm');
}
</script>

<template>
    <v-dialog v-model="open" max-width="440">
        <v-card>
            <v-card-title>{{ title }}</v-card-title>
            <v-card-text>{{ message }}</v-card-text>
            <v-card-actions>
                <v-spacer />
                <v-btn variant="text" :data-testid="`${testIdPrefix}-cancel`" @click="open = false">{{ t('common.cancel') }}</v-btn>
                <v-btn color="error" variant="flat" :data-testid="`${testIdPrefix}-confirm`" @click="confirm">{{
                    confirmLabel ?? t('common.delete')
                }}</v-btn>
            </v-card-actions>
        </v-card>
    </v-dialog>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';

const auth = useAuthStore();
const router = useRouter();

onMounted(async () => {
    await auth.completeLogin();
    await router.replace('/');
});
</script>

<template>
    <v-progress-circular indeterminate />
</template>
